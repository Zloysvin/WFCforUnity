using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

// класс модуль является представлением одного из возможных вариантов того что может находиться на клеточке
[System.Serializable]
public class Module
{
    // у каждого модуля есть объект который он представляет и который будет создан в мире когда этот модуль будет выбран
    public GameObject value;
    // массив сокетов каждого модуля. идет по часовой стрелки, где 0 - вверх, 3 - лево
    public int[] Sockets = new int[4];
}

// класс что представляет собой тайл самой карты. имеет список возможных модулей. Если модуль один то означает что что тайл больше не в супер позиции
// и у него есть конкертный модуль который он воспринимает и который будет отображаться на его координатах
public class Tile
{
    // список всех возможных модулей
    public List<Module> Modules { get; set; }
    // переменная которая обозначает что это клеточка уже была активирована и она повлияла на соседние от нее клеточки.
    // это нужно для того, чтобы избежать случаев, когда алгоритм будет останавливаться потому что так сложились обстоятельства что
    // в этой клетке оказался лишь 1 возможный модуль и по этому она никак не появляла на соседние клетки
    public bool hasDeveloped = false;

    public Tile()
    {
    }
}

public class WFC : MonoBehaviour
{
    // списко всех доступных для генерации модулей
    public List<Module> allModules = new List<Module>();

    // высота и ширина карты
    public int height;
    public int width;

    // карта которая представлена тайлами
    private Tile[,] map;

    private Module[] previusModules;
    private int previusY;
    private int previusX;

    private int tryCount;
    private bool needToRegenerate = true;

    void Start()
    {
        // в методе старт у нас ициализируется сама карта, и происходит генерация
        GetModulesFromPrefabs();

        while (needToRegenerate)
        {
            map = new Tile[height, width];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    map[i, j] = new Tile();
                }
            }


            InsertRandom(1, 1);
            needToRegenerate = false;
            Solve();
            if (!needToRegenerate)
            {
                Draw();
            }
        }
    }

    // так как вводить значение сокетов в массиве немного не удобно, я практиковал создание отдельного класа под названием MonoModule который в свою
    // очередь хранит значение всех сокетов в более доступной форме. 
    private void GetModulesFromPrefabs()
    {
        foreach (var module in allModules)
        {
            MonoModule monoModule = module.value.GetComponent<MonoModule>();
            module.Sockets = new[]
                { monoModule.socketTop, monoModule.socketRight, monoModule.socketBottom, monoModule.socketLeft };
        }
    }

    // метод который проверяет сгенерирована ли карта. карта считается сгенерированной когда все клеточки нахллдятся не в супер позиции - т.е 
    // когда количество их модулей = 1
    private bool CheckIfSolved()
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                if (map[i, j].Modules == null || map[i, j].Modules.Count != 1)
                    return false;
            }
        }

        return true;
    }

    // Основной метод генерации
    public void Solve()
    {
        while (!CheckIfSolved())
        {
            // индексы тайла с найменьшим количеством модулей
            int minY = -1;
            int minX = -1;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    // проверяем что модули у данного тайла существуют и если существуют, то что тайл не был активирован
                    if (map[i, j].Modules != null && !map[i, j].hasDeveloped)// && map[i, j].Modules.Count > 1)
                    {
                        if (minX > -1 && minY > -1)
                        {
                            if (map[minY, minX].Modules.Count > map[i, j].Modules.Count && map[minY, minX].Modules.Count != map[i, j].Modules.Count)
                            {
                                minY = i;
                                minX = j;
                            }
                        }
                        else
                        {
                            minY = i;
                            minX = j;
                        }
                    }
                }
            }
            // если минимальное значение было найдено, мы его калапсируем
            if (minX > -1 && minY > -1)
                Collapse(minY, minX);

            if (needToRegenerate)
                break;
        }
    }

    // метод калапсации определенного тайла
    private void Collapse(int y, int x)
    {

        if (map[y, x].Modules.Count != 0)
        {
            map[y, x].hasDeveloped = true;
            System.Random rnd = new System.Random();

            // выбираем случайны модуль из набора модулей этого тайла
            Module module = map[y, x].Modules[rnd.Next(map[y, x].Modules.Count)];
            previusModules = map[y, x].Modules.ToArray();
            previusY = y;
            previusX = x;
            // удаляем все предыдущие модули 
            map[y, x].Modules.Clear();
            // добавляем выбранный модуль как единственный
            map[y, x].Modules.Add(module);

            // задаем соседние клеточки по вертикали и горизонтале
            SetNeighborCells(y, x, -1, 0, module);
            SetNeighborCells(y, x, 0, 1, module);
            SetNeighborCells(y, x, 1, 0, module);
            SetNeighborCells(y, x, 0, -1, module);

            // задаем модули клеток по диагонали учитывая соседние от нее клетки. это нужно для избежание конфликтов сокетов
            FixDiagonals(y, x, -1, 1);
            FixDiagonals(y, x, 1, 1);
            FixDiagonals(y, x, 1, -1);
            FixDiagonals(y, x, -1, -1);
        }
        else
        {
            if (tryCount >= 5)
            {
                needToRegenerate = true;
                return;
            }
            Debug.Log($"Try Count no {tryCount + 1}");
            tryCount++;
            map[previusY, previusX].hasDeveloped = false;
            map[previusY, previusX].Modules.Clear();
            map[previusY, previusX].Modules.AddRange(previusModules);
        }
    }

    // методы для здачи клетки по вертикали или горизонатале
    private void SetNeighborCells(int y, int x, int yMod, int xMod, Module module)
    {
        // проверяем если клетка которую мы хотимм задать не нарушает никакие правила
        if (y + yMod >= 0 && y + yMod < height && x + xMod >= 0 && x + xMod < width)
        {
            // учитывая значения которые мы задали мы определяяем индексы сокетов исходной и целевой клеток
            int currentSocket = math.abs(yMod) * (1 + yMod) + math.abs(xMod) * (2 - xMod);
            int targetSocket = math.abs(yMod) * (1 - yMod) + math.abs(xMod) * (2 + xMod);
            // проверяем на то что у цеоевой клетки есть список модулей и что их количетво больше 1
            if (map[y + yMod, x + xMod].Modules is { Count: > 1 })
            {
                // создаем список модулей которые надо удалить и добавляем туда все модули целевой клетки целевой сокет которой не совпадает
                // с исходным сокетом
                List<Module> modulesToDelete = map[y + yMod, x + xMod].Modules
                    .Where(mod => module.Sockets[currentSocket] != mod.Sockets[targetSocket]).ToList();

                // удаляем все модули из списка на удаление
                foreach (var mod in modulesToDelete)
                {
                    map[y + yMod, x + xMod].Modules.Remove(mod);
                }
            }
            else if (map[y + yMod, x + xMod].Modules == null)
            {
                // если клетка не обладает никакими модулями, то это означает что она никогда не разрабатывалась.
                // создаем новый список модулей для данной клетки
                map[y + yMod, x + xMod].Modules = new List<Module>();

                // добавляем все модули из списка всех модулей, целейво сокет которых совпадает со значением исходного в исходной клетке
                foreach (var mod in allModules.Where(mod => module.Sockets[currentSocket] == mod.Sockets[targetSocket]))
                {
                    map[y + yMod, x + xMod].Modules.Add(mod);
                }
            }
        }
    }
    // метод который задает значение модулйе диагоналей, на основе сокетов его соседних клеток
    private void FixDiagonals(int y, int x, int yMod, int xMod)
    {
        // проверяем если клетка которую мы хотимм задать не нарушает никакие правила
        if (y + yMod >= 0 && y + yMod < height && x + xMod >= 0 && x + xMod < width)
        {
            // находим значение индексов сокетов по горизонтале (правый или левый) и по вертикале (верх или низ) для сокета по диагонале
            // и для соседних сокетов
            int currentSocketVert = math.abs(yMod) * (1 - yMod);
            int currentSocketHoriz = math.abs(xMod) * (2 + xMod);

            int targetSocketVert = math.abs(yMod) * (1 + yMod);
            int targetSocketHoriz = math.abs(xMod) * (2 - xMod);

            // проверяем что клетка по диагонале имеет модули и если имеет что их количество больше 1
            if (map[y + yMod, x + xMod].Modules is { Count: > 1 })
            {

                List<Module> modulesToAdd = new List<Module>();
                // проходим по каждому модулю вертикальной клеточки
                foreach (var mod1 in map[y + yMod, x].Modules)
                {
                    // проходим по каждому модулю исходной клеточке
                    foreach (var moduleDiag in map[y + yMod, x + xMod].Modules)
                    {
                        // проверяем если противоположные сокеты целевого и исходного модулей равны
                        if (moduleDiag.Sockets[currentSocketHoriz] == mod1.Sockets[targetSocketHoriz])
                        {
                            // проходим по каждому модулю горизонтальной клеточки
                            foreach (var mod2 in map[y, x + xMod].Modules)
                            {
                                // проверяем если противоположные сокеты целевого и исходного модулей равны
                                if (moduleDiag.Sockets[currentSocketVert] == mod2.Sockets[targetSocketVert])
                                {
                                    // проверяем что мы уже не добавили тот же самый модуль уже
                                    if (!modulesToAdd.Contains(moduleDiag))
                                    {
                                        modulesToAdd.Add(moduleDiag);
                                    }
                                }
                            }
                        }
                    }
                }

                map[y + yMod, x + xMod].Modules = modulesToAdd;
            }
            else if (map[y + yMod, x + xMod].Modules == null)
            {
                // если модулей у клетки по диагонали нету и она не была активирована еще
                map[y + yMod, x + xMod].Modules = new List<Module>();

                // проходимся по всем доступным модулям
                foreach (var allMod in allModules)
                {
                    // проходим по каждому модулю вертикальной клетки
                    foreach (var mod1 in map[y + yMod, x].Modules)
                    {
                        // проверяем если противоположные сокеты целевого и исходного модулей равны
                        if (mod1.Sockets[targetSocketHoriz] == allMod.Sockets[currentSocketHoriz])
                        {
                            // проходим по каждому модулю вертикальной клетки
                            foreach (var mod2 in map[y, x + xMod].Modules)
                            {
                                // проверяем если противоположные сокеты целевого и исходного модулей равны а так же что этот модуль не был до сих пор записан
                                if (mod2.Sockets[targetSocketVert] == allMod.Sockets[currentSocketVert] && !map[y + yMod, x + xMod].Modules.Contains(allMod))
                                {
                                    map[y + yMod, x + xMod].Modules.Add(allMod);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // метод который вставляет случайный модуль по указаным кординатам и колапсирует его
    private void InsertRandom(int y, int x)
    {
        map[y, x].Modules = new List<Module>();
        map[y, x].Modules.AddRange(allModules);
        map[y, x].index = 0;
        Collapse(y, x);
    }

    // метод для отрисовки карты
    private void Draw()
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                System.Random rnd = new System.Random();
                Instantiate(map[i, j].Modules[0].value, new Vector3(j * 1.1f, -1.1f * i, 0), map[i, j].Modules[0].value.transform.rotation);
            }
        }
    }
}
