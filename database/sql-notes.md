# SQL notes

> Учебная БД: `PracticeDB`. Скрипт создания и заполнения — [`database/init.sql`](init.sql).
> СУБД: Microsoft SQL Server 2025 Express (17.0.1000.7).
> Запросы выполнялись через `sqlcmd` 31.10.2026; в SSMS результаты будут теми же.
> Для заданий 28–30 — консольное приложение [`src/PracticeDb.App`](../src/PracticeDb.App).

## Содержание

- [Task 01 — Создание базы и таблиц](#task-01--создание-базы-и-таблиц)
- [Task 02 — INSERT: заполнение данными](#task-02--insert-заполнение-данными)
- [Task 03 — SELECT + WHERE](#task-03--select--where-базовые-выборки)
- [Task 04 — ORDER BY + TOP](#task-04--order-by--top-сортировка-и-лимит)
- [Task 05 — Агрегация](#task-05--агрегация-count-sum-avg-min-max)
- [Task 06 — GROUP BY](#task-06--group-by-статистика-по-отделам)
- [Task 07 — GROUP BY + HAVING](#task-07--group-by--having-фильтрация-групп)
- [Task 08 — INNER JOIN](#task-08--inner-join-сотрудники-и-отделы)
- [Task 09 — LEFT JOIN](#task-09--left-join-сотрудники-без-проектов)
- [Task 10 — Множественный JOIN](#task-10--множественный-join-сотрудники-отделы-проекты)
- [Task 11 — Self-join](#task-11--self-join-иерархия-руководителей)
- [Task 12 — Подзапросы в WHERE](#task-12--подзапросы-в-where)
- [Task 13 — Скалярные подзапросы в SELECT](#task-13--скалярные-подзапросы-в-select)
- [Task 14 — EXISTS / NOT EXISTS](#task-14--exists--not-exists)
- [Task 15 — UNION / UNION ALL](#task-15--union--union-all)
- [Task 16 — UPDATE](#task-16--update-изменение-данных)
- [Task 17 — DELETE](#task-17--delete-удаление-данных)
- [Task 18 — Транзакции](#task-18--транзакции-begin-tran--commit--rollback)
- [Task 19 — Представления](#task-19--представления-view)
- [Task 20 — Индексы](#task-20--индексы-создание-и-анализ)
- [Task 21 — Хранимые процедуры](#task-21--хранимые-процедуры)
- [Task 22 — Функции](#task-22--функции-скалярная-и-табличная)
- [Task 23 — Оконные функции](#task-23--оконные-функции-row_number-rank-dense_rank)
- [Task 24 — Оконные агрегаты](#task-24--оконные-агрегаты-sum-over-avg-over)
- [Task 25 — CTE](#task-25--cte-common-table-expressions)
- [Task 26 — CASE](#task-26--case-условная-логика-в-select)
- [Task 27 — Работа с датами](#task-27--работа-с-датами-dateadd-datediff-format)
- [Task 28 — C# + SQL: подключение и SELECT](#task-28--c--sql-подключение-и-select)
- [Task 29 — C# + SQL: параметризованный INSERT](#task-29--c--sql-параметризованный-insert)
- [Task 30 — C# + SQL: мини-отчёт по отделам](#task-30--c--sql-мини-отчёт-по-отделам)

---

## Task 01 — Создание базы и таблиц

### SQL

Полный скрипт находится в [`database/init.sql`](init.sql). Он идемпотентен: при повторном запуске удаляет `PracticeDB` и создаёт её заново.

```sql
IF DB_ID('PracticeDB') IS NOT NULL
BEGIN
    ALTER DATABASE PracticeDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PracticeDB;
END
GO

CREATE DATABASE PracticeDB;
GO

USE PracticeDB;
GO

CREATE TABLE Departments (
    DepartmentId   INT PRIMARY KEY IDENTITY(1,1),
    DepartmentName NVARCHAR(100) NOT NULL
);

CREATE TABLE Employees (
    EmployeeId   INT PRIMARY KEY IDENTITY(1,1),
    EmployeeName NVARCHAR(100) NOT NULL,
    Salary       DECIMAL(10,2) NOT NULL,
    HireDate     DATE NOT NULL,
    DepartmentId INT NOT NULL,
    ManagerId    INT NULL,
    CONSTRAINT FK_Employees_Departments FOREIGN KEY (DepartmentId)
        REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_Employees_Manager FOREIGN KEY (ManagerId)
        REFERENCES Employees(EmployeeId)
);

CREATE TABLE Projects (
    ProjectId   INT PRIMARY KEY IDENTITY(1,1),
    ProjectName NVARCHAR(100) NOT NULL,
    StartDate   DATE NOT NULL
);

CREATE TABLE EmployeeProjects (
    EmployeeId INT NOT NULL,
    ProjectId  INT NOT NULL,
    Hours      INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_EmployeeProjects PRIMARY KEY (EmployeeId, ProjectId),
    CONSTRAINT FK_EP_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId),
    CONSTRAINT FK_EP_Projects  FOREIGN KEY (ProjectId)  REFERENCES Projects(ProjectId)
);
GO
```

Проверка структуры:

```sql
SELECT
    t.name AS TableName,
    SUM(p.rows) AS [RowCount]
FROM sys.tables AS t
JOIN sys.partitions AS p
    ON p.object_id = t.object_id AND p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY t.name;

SELECT
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS TableName,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys AS fk
ORDER BY fk.name;
```

### Результат

```text
TableName        RowCount
---------------- --------
Departments      4
EmployeeProjects 14
Employees        10
Projects         5

ForeignKeyName           TableName        ReferencedTable
------------------------ ---------------- ---------------
FK_Employees_Departments Employees        Departments
FK_Employees_Manager     Employees        Employees
FK_EP_Employees          EmployeeProjects Employees
FK_EP_Projects           EmployeeProjects Projects
```

### Вывод

База и все четыре таблицы созданы. Первичные ключи, внешние ключи и связь `Employees.ManagerId → Employees.EmployeeId` работают. Скрипт можно запускать повторно — это удобно для проверки заданий с нуля.

---

## Task 02 — INSERT: заполнение данными

### SQL

Данные вставляются в `init.sql` одним `INSERT` с несколькими `VALUES` для каждой таблицы:

```sql
INSERT INTO Departments (DepartmentName) VALUES
    (N'Разработка'),
    (N'Аналитика'),
    (N'Тестирование'),
    (N'Менеджмент');

INSERT INTO Employees (EmployeeName, Salary, HireDate, DepartmentId, ManagerId) VALUES
    (N'Анна Иванова',    65000, '2020-03-15', 1, NULL),
    (N'Пётр Сидоров',    72000, '2019-07-01', 1, NULL),
    (N'Мария Кузнецова', 55000, '2021-11-20', 1, NULL),
    (N'Иван Петров',     48000, '2023-02-10', 1, NULL),
    (N'Ольга Смирнова',  61000, '2020-09-05', 2, NULL),
    (N'Дмитрий Орлов',   58000, '2022-04-18', 2, NULL),
    (N'Елена Волкова',   52000, '2023-06-01', 2, NULL),
    (N'Сергей Морозов',  45000, '2021-12-12', 3, NULL),
    (N'Наталья Соколова',43000, '2024-01-15', 3, NULL),
    (N'Алексей Лебедев', 90000, '2018-05-25', 4, NULL);

INSERT INTO Projects (ProjectName, StartDate) VALUES
    (N'CRM System',      '2023-01-10'),
    (N'Mobile App',      '2023-05-01'),
    (N'Data Warehouse',  '2022-11-20'),
    (N'AI Assistant',    '2024-02-01'),
    (N'Legacy Refactor', '2021-06-15');

INSERT INTO EmployeeProjects (EmployeeId, ProjectId, Hours) VALUES
    (1, 1, 120), (1, 3, 80),
    (2, 1, 150), (2, 4, 60),
    (3, 2, 100), (4, 2, 90),
    (4, 5, 40),  (5, 3, 110),
    (6, 3, 70),  (6, 4, 50),
    (7, 2, 95),  (8, 5, 130),
    (10, 4, 200),(10, 1, 30);
```

Дополнительно `init.sql` задаёт руководителей для четырёх сотрудников:

```sql
UPDATE Employees SET ManagerId = 1  WHERE EmployeeId = 4;
UPDATE Employees SET ManagerId = 2  WHERE EmployeeId = 3;
UPDATE Employees SET ManagerId = 5  WHERE EmployeeId = 6;
UPDATE Employees SET ManagerId = 10 WHERE EmployeeId = 9;
```

Проверка количества строк:

```sql
SELECT
    (SELECT COUNT(*) FROM Departments)      AS Departments,
    (SELECT COUNT(*) FROM Employees)        AS Employees,
    (SELECT COUNT(*) FROM Projects)         AS Projects,
    (SELECT COUNT(*) FROM EmployeeProjects) AS EmployeeProjects;
```

### Результат

```text
Departments Employees Projects EmployeeProjects
----------- --------- -------- ----------------
4           10        5        14
```

### Вывод

Все таблицы заполнены тестовыми данными. Количество строк совпадает с ожидаемым: 4 отдела, 10 сотрудников, 5 проектов и 14 назначений.

---

## Task 03 — SELECT + WHERE: базовые выборки

### SQL

```sql
-- 3.1 Зарплата больше 50000
SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE Salary > 50000
ORDER BY EmployeeId;

-- 3.2 Нанятые в 2023 году
SELECT EmployeeId, EmployeeName, HireDate
FROM Employees
WHERE YEAR(HireDate) = 2023
ORDER BY EmployeeId;

-- 3.3 Имя начинается на «А»
SELECT EmployeeId, EmployeeName
FROM Employees
WHERE EmployeeName LIKE N'А%'
ORDER BY EmployeeId;

-- 3.4 Сотрудники отдела 2
SELECT EmployeeId, EmployeeName, DepartmentId
FROM Employees
WHERE DepartmentId = 2
ORDER BY EmployeeId;

-- 3.5 Зарплата в диапазоне 40000–60000
SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE Salary BETWEEN 40000 AND 60000
ORDER BY Salary;
```

### Результат

```text
-- 3.1: 7 строк
EmployeeId EmployeeName     Salary
---------- ---------------- ------
1          Анна Иванова     65000.00
2          Пётр Сидоров     72000.00
3          Мария Кузнецова  55000.00
5          Ольга Смирнова   61000.00
6          Дмитрий Орлов    58000.00
7          Елена Волкова    52000.00
10         Алексей Лебедев  90000.00

-- 3.2: 2 строки
EmployeeId EmployeeName   HireDate
---------- -------------- ----------
4          Иван Петров    2023-02-10
7          Елена Волкова  2023-06-01

-- 3.3: 2 строки
EmployeeId EmployeeName
---------- ----------------
1          Анна Иванова
10         Алексей Лебедев

-- 3.4: 3 строки
EmployeeId EmployeeName     DepartmentId
---------- ---------------- ------------
5          Ольга Смирнова   2
6          Дмитрий Орлов    2
7          Елена Волкова    2

-- 3.5: 6 строк
EmployeeId EmployeeName      Salary
---------- ----------------- ------
9          Наталья Соколова  43000.00
8          Сергей Морозов    45000.00
4          Иван Петров       48000.00
7          Елена Волкова     52000.00
3          Мария Кузнецова   55000.00
6          Дмитрий Орлов     58000.00
```

### Вывод

Все пять условий отработали корректно. Для поиска по первому символу удобно использовать `LIKE N'А%'`; для диапазона — `BETWEEN`, который включает обе границы.

---

## Task 04 — ORDER BY + TOP: сортировка и лимит

### SQL

```sql
-- 4.1 Топ-3 по зарплате
SELECT TOP (3) EmployeeId, EmployeeName, Salary
FROM Employees
ORDER BY Salary DESC;

-- 4.2 Топ-5 самых давних сотрудников
SELECT TOP (5) EmployeeId, EmployeeName, HireDate
FROM Employees
ORDER BY HireDate ASC;

-- 4.3 Сортировка по отделу, затем по зарплате
SELECT EmployeeId, EmployeeName, DepartmentId, Salary
FROM Employees
ORDER BY DepartmentId ASC, Salary DESC;
```

### Результат

```text
-- 4.1
EmployeeId EmployeeName     Salary
---------- ---------------- ------
10         Алексей Лебедев  90000.00
2          Пётр Сидоров     72000.00
1          Анна Иванова     65000.00

-- 4.2
EmployeeId EmployeeName      HireDate
---------- ----------------- ----------
10         Алексей Лебедев   2018-05-25
2          Пётр Сидоров      2019-07-01
1          Анна Иванова      2020-03-15
5          Ольга Смирнова    2020-09-05
3          Мария Кузнецова   2021-11-20

-- 4.3
EmployeeId EmployeeName      DepartmentId Salary
---------- ----------------- ------------ ------
2          Пётр Сидоров      1            72000.00
1          Анна Иванова      1            65000.00
3          Мария Кузнецова   1            55000.00
4          Иван Петров       1            48000.00
5          Ольга Смирнова    2            61000.00
6          Дмитрий Орлов     2            58000.00
7          Елена Волкова     2            52000.00
8          Сергей Морозов    3            45000.00
9          Наталья Соколова  3            43000.00
10         Алексей Лебедев   4            90000.00
```

### Вывод

`TOP` без `ORDER BY` не гарантирует порядок, поэтому лимит всегда указан вместе с сортировкой. В 4.3 использована составная сортировка: сначала отдел по возрастанию, затем зарплата по убыванию.

---

## Task 05 — Агрегация: COUNT, SUM, AVG, MIN, MAX

### SQL

```sql
SELECT
    COUNT(*)                    AS EmployeeCount,
    SUM(Salary)                 AS TotalSalary,
    CAST(ROUND(AVG(Salary), 2) AS DECIMAL(10,2)) AS AvgSalary,
    MIN(Salary)                 AS MinSalary,
    MAX(Salary)                 AS MaxSalary,
    SUM(CASE WHEN Salary > 50000 THEN 1 ELSE 0 END) AS Above50000
FROM Employees;
```

### Результат

```text
EmployeeCount TotalSalary AvgSalary MinSalary MaxSalary Above50000
------------- ----------- --------- --------- --------- ----------
10            589000.00   58900.00  43000.00  90000.00  7
```

### Вывод

Все пять агрегатов (и дополнительный счётчик через `CASE`) посчитаны одним запросом. `AVG` возвращает `decimal` с большой шкалой, поэтому результат явно приведён к `DECIMAL(10,2)`.

---

## Task 06 — GROUP BY: статистика по отделам

### SQL

```sql
SELECT
    e.DepartmentId,
    d.DepartmentName,
    COUNT(*)                    AS EmployeeCount,
    CAST(ROUND(AVG(e.Salary), 2) AS DECIMAL(10,2)) AS AvgSalary,
    SUM(e.Salary)               AS TotalSalary
FROM Employees AS e
INNER JOIN Departments AS d ON d.DepartmentId = e.DepartmentId
GROUP BY e.DepartmentId, d.DepartmentName
ORDER BY AvgSalary DESC;
```

### Результат

```text
DepartmentId DepartmentName EmployeeCount AvgSalary TotalSalary
------------ -------------- ------------- --------- -----------
4            Менеджмент     1             90000.00  90000.00
1            Разработка     4             60000.00  240000.00
2            Аналитика      3             57000.00  171000.00
3            Тестирование   2             44000.00  88000.00
```

### Вывод

Для каждого отдела получена ровно одна строка с количеством сотрудников, средней и суммарной зарплатой. Сортировка по средней зарплате по убыванию работает.

---

## Task 07 — GROUP BY + HAVING: фильтрация групп

### SQL

```sql
-- 7.1 Больше двух сотрудников
SELECT
    e.DepartmentId,
    d.DepartmentName,
    COUNT(*) AS EmployeeCount
FROM Employees AS e
INNER JOIN Departments AS d ON d.DepartmentId = e.DepartmentId
GROUP BY e.DepartmentId, d.DepartmentName
HAVING COUNT(*) > 2
ORDER BY e.DepartmentId;

-- 7.2 Средняя зарплата больше 55000
SELECT
    e.DepartmentId,
    d.DepartmentName,
    CAST(ROUND(AVG(e.Salary), 2) AS DECIMAL(10,2)) AS AvgSalary
FROM Employees AS e
INNER JOIN Departments AS d ON d.DepartmentId = e.DepartmentId
GROUP BY e.DepartmentId, d.DepartmentName
HAVING AVG(e.Salary) > 55000
ORDER BY AvgSalary DESC;

-- 7.3 Суммарная зарплата больше 150000
SELECT
    e.DepartmentId,
    d.DepartmentName,
    SUM(e.Salary) AS TotalSalary
FROM Employees AS e
INNER JOIN Departments AS d ON d.DepartmentId = e.DepartmentId
GROUP BY e.DepartmentId, d.DepartmentName
HAVING SUM(e.Salary) > 150000
ORDER BY TotalSalary DESC;
```

### Результат

```text
-- 7.1
DepartmentId DepartmentName EmployeeCount
------------ -------------- -------------
1            Разработка     4
2            Аналитика      3

-- 7.2
DepartmentId DepartmentName AvgSalary
------------ -------------- ---------
4            Менеджмент     90000.00
1            Разработка     60000.00
2            Аналитика      57000.00

-- 7.3
DepartmentId DepartmentName TotalSalary
------------ -------------- -----------
1            Разработка     240000.00
2            Аналитика      171000.00
```

### Вывод

`WHERE` фильтрует строки до группировки, а `HAVING` — уже сгруппированные данные. Все три условия применены именно в `HAVING`, как требует задание.

---

## Task 08 — INNER JOIN: сотрудники и отделы

### SQL

```sql
SELECT
    e.EmployeeName,
    d.DepartmentName,
    e.Salary
FROM Employees AS e
INNER JOIN Departments AS d ON d.DepartmentId = e.DepartmentId
ORDER BY d.DepartmentName ASC, e.Salary DESC;
```

### Результат

```text
EmployeeName      DepartmentName Salary
----------------- -------------- ------
Ольга Смирнова    Аналитика      61000.00
Дмитрий Орлов     Аналитика      58000.00
Елена Волкова     Аналитика      52000.00
Алексей Лебедев   Менеджмент     90000.00
Пётр Сидоров      Разработка     72000.00
Анна Иванова      Разработка     65000.00
Мария Кузнецова   Разработка     55000.00
Иван Петров       Разработка     48000.00
Сергей Морозов    Тестирование   45000.00
Наталья Соколова  Тестирование   43000.00
```

### Вывод

`INNER JOIN` вернул всех 10 сотрудников, у каждого есть название отдела. Строк без отдела нет, потому что `Employees.DepartmentId` обязателен (`NOT NULL`) и защищён внешним ключом.

---

## Task 09 — LEFT JOIN: сотрудники без проектов

### SQL

```sql
SELECT
    e.EmployeeId,
    e.EmployeeName
FROM Employees AS e
LEFT JOIN EmployeeProjects AS ep ON ep.EmployeeId = e.EmployeeId
WHERE ep.EmployeeId IS NULL
ORDER BY e.EmployeeId;
```

### Результат

```text
EmployeeId EmployeeName
---------- ----------------
9          Наталья Соколова
```

### Вывод

`LEFT JOIN` сохраняет всех сотрудников, а условие `ep.EmployeeId IS NULL` оставляет только тех, для кого не нашлось ни одного назначения. В тестовых данных это Наталья Соколова.

---

## Task 10 — Множественный JOIN: сотрудники, отделы, проекты

### SQL

```sql
SELECT
    e.EmployeeName,
    d.DepartmentName,
    p.ProjectName,
    ep.Hours
FROM Employees AS e
INNER JOIN Departments AS d ON d.DepartmentId = e.DepartmentId
INNER JOIN EmployeeProjects AS ep ON ep.EmployeeId = e.EmployeeId
INNER JOIN Projects AS p ON p.ProjectId = ep.ProjectId
ORDER BY d.DepartmentName ASC, e.EmployeeName ASC, p.ProjectName ASC;
```

### Результат

```text
EmployeeName      DepartmentName ProjectName     Hours
----------------- -------------- --------------- -----
Дмитрий Орлов     Аналитика      AI Assistant    50
Дмитрий Орлов     Аналитика      Data Warehouse  70
Елена Волкова     Аналитика      Mobile App      95
Ольга Смирнова    Аналитика      Data Warehouse  110
Алексей Лебедев   Менеджмент     AI Assistant    200
Алексей Лебедев   Менеджмент     CRM System      30
Анна Иванова      Разработка     CRM System      120
Анна Иванова      Разработка     Data Warehouse  80
Иван Петров       Разработка     Legacy Refactor 40
Иван Петров       Разработка     Mobile App      90
Мария Кузнецова   Разработка     Mobile App      100
Пётр Сидоров      Разработка     AI Assistant    60
Пётр Сидоров      Разработка     CRM System      150
Сергей Морозов    Тестирование   Legacy Refactor 130
```

### Вывод

Четыре таблицы соединены в правильной цепочке. Получилось 14 строк — ровно столько, сколько записей в `EmployeeProjects`. Сортировка по отделу, сотруднику и проекту соблюдена.

## Task 11 — Self-join: иерархия руководителей

### SQL

```sql
SELECT
    e.EmployeeId,
    e.EmployeeName,
    m.EmployeeName AS ManagerName
FROM Employees AS e
LEFT JOIN Employees AS m ON m.EmployeeId = e.ManagerId
ORDER BY e.EmployeeId;
```

### Результат

```text
EmployeeId EmployeeName      ManagerName
---------- ----------------- -----------
1          Анна Иванова      NULL
2          Пётр Сидоров      NULL
3          Мария Кузнецова   Пётр Сидоров
4          Иван Петров       Анна Иванова
5          Ольга Смирнова    NULL
6          Дмитрий Орлов     Ольга Смирнова
7          Елена Волкова     NULL
8          Сергей Морозов    NULL
9          Наталья Соколова  Алексей Лебедев
10         Алексей Лебедев   NULL
```

### Вывод

Таблица `Employees` соединена сама с собой по `ManagerId → EmployeeId`. `LEFT JOIN` сохраняет сотрудников без руководителя, у них `ManagerName` равно `NULL`.

---

## Task 12 — Подзапросы в WHERE

### SQL

```sql
-- 12.1 Зарплата выше средней
SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE Salary > (SELECT AVG(Salary) FROM Employees)
ORDER BY Salary DESC;

-- 12.2 Максимальная зарплата
SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE Salary = (SELECT MAX(Salary) FROM Employees);
```

### Результат

```text
-- 12.1: 4 строки (средняя зарплата = 58900.00)
EmployeeId EmployeeName     Salary
---------- ---------------- ------
10         Алексей Лебедев  90000.00
2          Пётр Сидоров     72000.00
1          Анна Иванова     65000.00
5          Ольга Смирнова   61000.00

-- 12.2: 1 строка
EmployeeId EmployeeName     Salary
---------- ---------------- ------
10         Алексей Лебедев  90000.00
```

### Вывод

Скалярный подзапрос возвращает одно значение и может использоваться в `WHERE` как обычное число. В 12.2 сравнение с `MAX` даёт сотрудника с максимальной зарплатой.

---

## Task 13 — Скалярные подзапросы в SELECT

### SQL

```sql
SELECT
    e.EmployeeId,
    e.EmployeeName,
    e.Salary,
    e.DepartmentId,
    CAST(ROUND((
        SELECT AVG(e2.Salary)
        FROM Employees AS e2
        WHERE e2.DepartmentId = e.DepartmentId
    ), 2) AS DECIMAL(10,2)) AS DepartmentAvg,
    CAST(ROUND(
        e.Salary - (
            SELECT AVG(e2.Salary)
            FROM Employees AS e2
            WHERE e2.DepartmentId = e.DepartmentId
        ), 2) AS DECIMAL(10,2)) AS Difference
FROM Employees AS e
ORDER BY e.DepartmentId, e.EmployeeId;
```

### Результат

```text
EmployeeId EmployeeName      Salary   DepartmentId DepartmentAvg Difference
---------- ----------------- -------  ------------ ------------- ----------
1          Анна Иванова      65000.00 1            60000.00      5000.00
2          Пётр Сидоров      72000.00 1            60000.00      12000.00
3          Мария Кузнецова   55000.00 1            60000.00      -5000.00
4          Иван Петров       48000.00 1            60000.00      -12000.00
5          Ольга Смирнова    61000.00 2            57000.00      4000.00
6          Дмитрий Орлов     58000.00 2            57000.00      1000.00
7          Елена Волкова     52000.00 2            57000.00      -5000.00
8          Сергей Морозов    45000.00 3            44000.00      1000.00
9          Наталья Соколова  43000.00 3            44000.00      -1000.00
10         Алексей Лебедев   90000.00 4            90000.00      0.00
```

### Вывод

Коррелированный подзапрос вычисляет среднюю зарплату именно для отдела текущей строки. Отклонение считается как `Salary - DepartmentAvg`; у Алексея Лебедева оно равно нулю, потому что в отделе он один.

---

## Task 14 — EXISTS / NOT EXISTS

### SQL

```sql
-- 14.1 Есть хотя бы один проект
SELECT e.EmployeeId, e.EmployeeName
FROM Employees AS e
WHERE EXISTS (
    SELECT 1
    FROM EmployeeProjects AS ep
    WHERE ep.EmployeeId = e.EmployeeId
)
ORDER BY e.EmployeeId;

-- 14.2 Нет проектов
SELECT e.EmployeeId, e.EmployeeName
FROM Employees AS e
WHERE NOT EXISTS (
    SELECT 1
    FROM EmployeeProjects AS ep
    WHERE ep.EmployeeId = e.EmployeeId
)
ORDER BY e.EmployeeId;
```

### Результат

```text
-- 14.1: 9 строк
EmployeeId EmployeeName
---------- -----------------
1          Анна Иванова
2          Пётр Сидоров
3          Мария Кузнецова
4          Иван Петров
5          Ольга Смирнова
6          Дмитрий Орлов
7          Елена Волкова
8          Сергей Морозов
10         Алексей Лебедев

-- 14.2: 1 строка
EmployeeId EmployeeName
---------- ----------------
9          Наталья Соколова
```

### Вывод

`EXISTS` проверяет только факт наличия строки и не требует соединения. Результаты согласуются с заданиями 8 и 9: 9 сотрудников имеют проекты, Наталья Соколова — нет.

---

## Task 15 — UNION / UNION ALL

### SQL

```sql
-- 15.1 UNION: уникальные строки
SELECT EmployeeId, EmployeeName, Salary, HireDate
FROM Employees
WHERE Salary > 60000
UNION
SELECT EmployeeId, EmployeeName, Salary, HireDate
FROM Employees
WHERE HireDate < '2022-01-01'
ORDER BY EmployeeId;

-- 15.2 UNION ALL: с дубликатами
SELECT EmployeeId, EmployeeName, Salary, HireDate
FROM Employees
WHERE Salary > 60000
UNION ALL
SELECT EmployeeId, EmployeeName, Salary, HireDate
FROM Employees
WHERE HireDate < '2022-01-01'
ORDER BY EmployeeId;

-- 15.3 Сравнение количества строк
SELECT 'UNION' AS OperatorName, COUNT(*) AS RowsTotal
FROM (
    SELECT EmployeeId FROM Employees WHERE Salary > 60000
    UNION
    SELECT EmployeeId FROM Employees WHERE HireDate < '2022-01-01'
) AS u
UNION ALL
SELECT 'UNION ALL', COUNT(*)
FROM (
    SELECT EmployeeId FROM Employees WHERE Salary > 60000
    UNION ALL
    SELECT EmployeeId FROM Employees WHERE HireDate < '2022-01-01'
) AS ua;
```

### Результат

```text
-- 15.1 UNION: 6 строк
EmployeeId EmployeeName      Salary   HireDate
---------- ----------------- -------  ----------
1          Анна Иванова      65000.00 2020-03-15
2          Пётр Сидоров      72000.00 2019-07-01
3          Мария Кузнецова   55000.00 2021-11-20
5          Ольга Смирнова    61000.00 2020-09-05
8          Сергей Морозов    45000.00 2021-12-12
10         Алексей Лебедев   90000.00 2018-05-25

-- 15.2 UNION ALL: 10 строк (дубликаты сотрудников 1, 2, 5, 10)
EmployeeId EmployeeName      Salary   HireDate
---------- ----------------- -------  ----------
1          Анна Иванова      65000.00 2020-03-15
1          Анна Иванова      65000.00 2020-03-15
2          Пётр Сидоров      72000.00 2019-07-01
2          Пётр Сидоров      72000.00 2019-07-01
3          Мария Кузнецова   55000.00 2021-11-20
5          Ольга Смирнова    61000.00 2020-09-05
5          Ольга Смирнова    61000.00 2020-09-05
8          Сергей Морозов    45000.00 2021-12-12
10         Алексей Лебедев   90000.00 2018-05-25
10         Алексей Лебедев   90000.00 2018-05-25

-- 15.3
OperatorName RowsTotal
------------ ---------
UNION        6
UNION ALL    10
```

### Вывод

`UNION` удаляет дубликаты, поэтому остаётся 6 уникальных сотрудников. `UNION ALL` сохраняет все 10 строк: 4 строки из первого запроса и 6 из второго. Разница хорошо видна по количеству строк.

---

## Task 16 — UPDATE: изменение данных

### SQL

```sql
-- 16.1 Зарплата сотрудников отдела 1 +10%
SELECT EmployeeId, EmployeeName, Salary, DepartmentId
FROM Employees
WHERE DepartmentId = 1
ORDER BY EmployeeId;

UPDATE Employees
SET Salary = Salary * 1.10
WHERE DepartmentId = 1;
SELECT @@ROWCOUNT AS UpdatedRows;

SELECT EmployeeId, EmployeeName, Salary, DepartmentId
FROM Employees
WHERE DepartmentId = 1
ORDER BY EmployeeId;

-- 16.2 Зарплата сотрудников, нанятых до 2022 года, +5000
SELECT EmployeeId, EmployeeName, Salary, HireDate
FROM Employees
WHERE HireDate < '2022-01-01'
ORDER BY EmployeeId;

UPDATE Employees
SET Salary = Salary + 5000
WHERE HireDate < '2022-01-01';
SELECT @@ROWCOUNT AS UpdatedRows;

SELECT EmployeeId, EmployeeName, Salary, HireDate
FROM Employees
WHERE HireDate < '2022-01-01'
ORDER BY EmployeeId;

-- 16.3 Изменение даты найма сотрудника 4
SELECT EmployeeId, EmployeeName, HireDate
FROM Employees
WHERE EmployeeId = 4;

UPDATE Employees
SET HireDate = '2023-03-01'
WHERE EmployeeId = 4;
SELECT @@ROWCOUNT AS UpdatedRows;

SELECT EmployeeId, EmployeeName, HireDate
FROM Employees
WHERE EmployeeId = 4;
```

### Результат

```text
-- 16.1 до UPDATE
EmployeeId EmployeeName      Salary   DepartmentId
---------- ----------------- -------  ------------
1          Анна Иванова      65000.00 1
2          Пётр Сидоров      72000.00 1
3          Мария Кузнецова   55000.00 1
4          Иван Петров       48000.00 1

UpdatedRows
-----------
4

-- 16.1 после UPDATE (+10%)
EmployeeId EmployeeName      Salary   DepartmentId
---------- ----------------- -------  ------------
1          Анна Иванова      71500.00 1
2          Пётр Сидоров      79200.00 1
3          Мария Кузнецова   60500.00 1
4          Иван Петров       52800.00 1

-- 16.2 до UPDATE
EmployeeId EmployeeName      Salary   HireDate
---------- ----------------- -------  ----------
1          Анна Иванова      71500.00 2020-03-15
2          Пётр Сидоров      79200.00 2019-07-01
3          Мария Кузнецова   60500.00 2021-11-20
5          Ольга Смирнова    61000.00 2020-09-05
8          Сергей Морозов    45000.00 2021-12-12
10         Алексей Лебедев   90000.00 2018-05-25

UpdatedRows
-----------
6

-- 16.2 после UPDATE (+5000)
EmployeeId EmployeeName      Salary   HireDate
---------- ----------------- -------  ----------
1          Анна Иванова      76500.00 2020-03-15
2          Пётр Сидоров      84200.00 2019-07-01
3          Мария Кузнецова   65500.00 2021-11-20
5          Ольга Смирнова    66000.00 2020-09-05
8          Сергей Морозов    50000.00 2021-12-12
10         Алексей Лебедев   95000.00 2018-05-25

-- 16.3 до UPDATE
EmployeeId EmployeeName HireDate
---------- ------------ ----------
4          Иван Петров  2023-02-10

UpdatedRows
-----------
1

-- 16.3 после UPDATE
EmployeeId EmployeeName HireDate
---------- ------------ ----------
4          Иван Петров  2023-03-01
```

### Вывод

`UPDATE` затронул только целевые строки: 4 сотрудника отдела 1, 6 сотрудников с датой найма до 2022 года и одного сотрудника по `EmployeeId`. `SELECT @@ROWCOUNT` подтверждает количество изменённых строк. После задания база пересоздана через `init.sql`, чтобы дальнейшие задания выполнялись на исходных данных.

---

## Task 17 — DELETE: удаление данных

### SQL

```sql
-- 17.1 Назначения с Hours = 0
SELECT COUNT(*) AS ZeroHourAssignments
FROM EmployeeProjects
WHERE Hours = 0;

DELETE FROM EmployeeProjects
WHERE Hours = 0;
SELECT @@ROWCOUNT AS DeletedRows;

SELECT COUNT(*) AS RemainingAssignments
FROM EmployeeProjects;

-- 17.2 Удаление сотрудника 4 с его назначениями
SELECT EmployeeId, EmployeeName
FROM Employees
WHERE EmployeeId = 4;

SELECT EmployeeId, ProjectId, Hours
FROM EmployeeProjects
WHERE EmployeeId = 4;

DELETE FROM EmployeeProjects
WHERE EmployeeId = 4;
SELECT @@ROWCOUNT AS DeletedAssignments;

DELETE FROM Employees
WHERE EmployeeId = 4;
SELECT @@ROWCOUNT AS DeletedEmployees;

SELECT COUNT(*) AS EmployeesCount FROM Employees;
SELECT COUNT(*) AS AssignmentsCount FROM EmployeeProjects;
SELECT EmployeeId, EmployeeName FROM Employees WHERE EmployeeId = 4;

-- 17.3 DELETE с WHERE и без WHERE на тестовой копии
SELECT *
INTO Employees_Test
FROM Employees;

SELECT COUNT(*) AS TestRowsBefore FROM Employees_Test;

DELETE FROM Employees_Test
WHERE DepartmentId = 4;
SELECT @@ROWCOUNT AS DeletedWithWhere;

SELECT COUNT(*) AS TestRowsAfterWhere FROM Employees_Test;

DELETE FROM Employees_Test;
SELECT @@ROWCOUNT AS DeletedWithoutWhere;

SELECT COUNT(*) AS TestRowsAfterAll FROM Employees_Test;

DROP TABLE Employees_Test;
```

### Результат

```text
-- 17.1
ZeroHourAssignments
-------------------
0

DeletedRows
-----------
0

RemainingAssignments
--------------------
14

-- 17.2 до удаления
EmployeeId EmployeeName
---------- ------------
4          Иван Петров

EmployeeId ProjectId Hours
---------- --------- -----
4          2         90
4          5         40

DeletedAssignments
------------------
2

DeletedEmployees
----------------
1

-- 17.2 после удаления
EmployeesCount
--------------
9

AssignmentsCount
----------------
12

EmployeeId EmployeeName
---------- ------------
(0 строк)

-- 17.3 тестовая копия
TestRowsBefore
--------------
9

DeletedWithWhere
----------------
1

TestRowsAfterWhere
------------------
8

DeletedWithoutWhere
-------------------
8

TestRowsAfterAll
----------------
0
```

### Вывод

Записей с `Hours = 0` в тестовых данных нет, поэтому первый `DELETE` удалил 0 строк. Для сотрудника 4 сначала удалены назначения, затем сам сотрудник — внешний ключ не нарушен. На тестовой копии показаны оба варианта: `DELETE ... WHERE` удаляет только отдел 4, а `DELETE` без `WHERE` очищает всю таблицу. После задания база снова пересоздана через `init.sql`.

---

## Task 18 — Транзакции: BEGIN TRAN / COMMIT / ROLLBACK

### SQL

```sql
-- 18.1 ROLLBACK
SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE EmployeeId = 1;

BEGIN TRANSACTION;

UPDATE Employees
SET Salary = Salary + 1000
WHERE EmployeeId = 1;

SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE EmployeeId = 1;

SELECT @@TRANCOUNT AS TranCountBeforeRollback;

ROLLBACK TRANSACTION;

SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE EmployeeId = 1;

SELECT @@TRANCOUNT AS TranCountAfterRollback;

-- 18.2 COMMIT
BEGIN TRANSACTION;

UPDATE Employees
SET Salary = Salary + 1000
WHERE EmployeeId = 1;

SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE EmployeeId = 1;

COMMIT TRANSACTION;

SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE EmployeeId = 1;

SELECT @@TRANCOUNT AS TranCountAfterCommit;
```

### Результат

```text
-- до транзакции
EmployeeId EmployeeName  Salary
---------- ------------- ------
1          Анна Иванова  65000.00

-- внутри транзакции
EmployeeId EmployeeName  Salary
---------- ------------- ------
1          Анна Иванова  66000.00

TranCountBeforeRollback
-----------------------
1

-- после ROLLBACK
EmployeeId EmployeeName  Salary
---------- ------------- ------
1          Анна Иванова  65000.00

TranCountAfterRollback
----------------------
0

-- внутри второй транзакции
EmployeeId EmployeeName  Salary
---------- ------------- ------
1          Анна Иванова  66000.00

-- после COMMIT
EmployeeId EmployeeName  Salary
---------- ------------- ------
1          Анна Иванова  66000.00

TranCountAfterCommit
--------------------
0
```

### Вывод

После `ROLLBACK` значение вернулось к 65000.00, после `COMMIT` сохранилось 66000.00. `@@TRANCOUNT` показывает 1 внутри транзакции и 0 после завершения. После задания база пересоздана через `init.sql`.

---

## Task 19 — Представления (VIEW)

### SQL

```sql
CREATE VIEW vw_EmployeeDetails AS
SELECT
    e.EmployeeName,
    d.DepartmentName,
    p.ProjectName,
    ep.Hours
FROM Employees AS e
INNER JOIN Departments AS d ON d.DepartmentId = e.DepartmentId
INNER JOIN EmployeeProjects AS ep ON ep.EmployeeId = e.EmployeeId
INNER JOIN Projects AS p ON p.ProjectId = ep.ProjectId;
GO

SELECT *
FROM vw_EmployeeDetails
ORDER BY DepartmentName, EmployeeName, ProjectName;
GO

DROP VIEW vw_EmployeeDetails;
GO
```

### Результат

```text
EmployeeName      DepartmentName ProjectName     Hours
----------------- -------------- --------------- -----
Дмитрий Орлов     Аналитика      AI Assistant    50
Дмитрий Орлов     Аналитика      Data Warehouse  70
Елена Волкова     Аналитика      Mobile App      95
Ольга Смирнова    Аналитика      Data Warehouse  110
Алексей Лебедев   Менеджмент     AI Assistant    200
Алексей Лебедев   Менеджмент     CRM System      30
Анна Иванова      Разработка     CRM System      120
Анна Иванова      Разработка     Data Warehouse  80
Иван Петров       Разработка     Legacy Refactor 40
Иван Петров       Разработка     Mobile App      90
Мария Кузнецова   Разработка     Mobile App      100
Пётр Сидоров      Разработка     AI Assistant    60
Пётр Сидоров      Разработка     CRM System      150
Сергей Морозов    Тестирование   Legacy Refactor 130

(14 строк)
```

`DROP VIEW vw_EmployeeDetails;` выполнен без ошибок.

### Вывод

Представление инкапсулирует соединение четырёх таблиц и возвращает те же 14 строк, что и запрос из задания 10. После проверки представление удалено, как требует задание.

---

## Task 20 — Индексы: создание и анализ

### SQL

До создания индексов:

```sql
SET STATISTICS IO ON;
SELECT EmployeeId, EmployeeName, Salary, DepartmentId
FROM Employees
WHERE DepartmentId = 1 AND Salary > 50000;
SET STATISTICS IO OFF;
GO

SET SHOWPLAN_TEXT ON;
GO

SELECT EmployeeId, EmployeeName, Salary, DepartmentId
FROM Employees
WHERE DepartmentId = 1 AND Salary > 50000;
GO

SET SHOWPLAN_TEXT OFF;
GO
```

Создание индексов:

```sql
CREATE INDEX IX_Employees_DepartmentId ON Employees(DepartmentId);
GO

CREATE INDEX IX_Employees_Salary ON Employees(Salary);
GO
```

После создания индексов запрос и план выполнены повторно (тот же код, что выше).

### Результат

```text
-- До индексов: Clustered Index Scan, logical reads = 2
EmployeeId EmployeeName     Salary   DepartmentId
---------- ---------------- -------  ------------
1          Анна Иванова     65000.00 1
2          Пётр Сидоров     72000.00 1
3          Мария Кузнецова  55000.00 1

StmtText
--------
  |--Clustered Index Scan(OBJECT:([PracticeDB].[dbo].[Employees].[PK__Employee__...]),
        WHERE:([DepartmentId]=CONVERT_IMPLICIT(int,[@1],0)
          AND [Salary]>CONVERT_IMPLICIT(decimal(10,0),[@2],0)))

Таблица "Employees". Сканирований: 1, логических операций чтения: 2.

-- После создания индексов на 10 строках: план не изменился
StmtText
--------
  |--Clustered Index Scan(OBJECT:([PracticeDB].[dbo].[Employees].[PK__Employee__...]),
        WHERE:([DepartmentId]=(1) AND [Salary]>(50000.)))

Таблица "Employees". Сканирований: 1, логических операций чтения: 2.
```

На маленькой таблице из 10 строк оптимизатору дешевле просканировать кластерный индекс, чем делать seek + key lookup. Чтобы показать, что индексы реально используются, был выполнен дополнительный эксперимент: в `Employees` временно добавлены 20 000 строк, причём в отделе 1 оказалось только 20 строк. План сразу изменился (текст плана сокращён для читаемости):

```text
  |--Nested Loops(Inner Join, OUTER REFERENCES:([EmployeeId]))
       |--Hash Match(Inner Join, HASH:([EmployeeId])=([EmployeeId]))
       |    |--Index Seek(OBJECT:([Employees].[IX_Employees_DepartmentId]),
       |    |             SEEK:([DepartmentId]=(1)) ORDERED FORWARD)
       |    |--Index Seek(OBJECT:([Employees].[IX_Employees_Salary]),
       |    |             SEEK:([Salary] > (50000.)) ORDERED FORWARD)
       |--Clustered Index Seek(OBJECT:([Employees].[PK_Employees]),
                 SEEK:([EmployeeId]=[EmployeeId]) LOOKUP ORDERED FORWARD)

Таблица "Employees". Сканирований: 2, логических операций чтения: 12.
```

После эксперимента база пересоздана через `init.sql`, поэтому временные 20 000 строк в неё не попали.

### Вывод

На 10 строках разницы в `logical reads` нет — это ожидаемо: таблица занимает всего 2 страницы. На большем объёме данных с хорошей селективностью SQL Server использует оба некластерных индекса: `Index Seek` по `DepartmentId` и `Salary`, затем key lookup в кластерный индекс. Главный вывод: индекс помогает не всегда, а когда оптимизатор считает его дешевле сканирования.

---

## Task 21 — Хранимые процедуры

### SQL

```sql
CREATE PROCEDURE sp_GetEmployeesByDepartment
    @DeptId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT EmployeeId, EmployeeName, Salary, DepartmentId
    FROM Employees
    WHERE DepartmentId = @DeptId
    ORDER BY EmployeeId;
END;
GO

EXEC sp_GetEmployeesByDepartment @DeptId = 1;
GO

CREATE PROCEDURE sp_GetAvgSalaryByDepartment
    @DeptId INT,
    @AvgSalary DECIMAL(10,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @AvgSalary = AVG(Salary)
    FROM Employees
    WHERE DepartmentId = @DeptId;
END;
GO

DECLARE @Average DECIMAL(10,2);

EXEC sp_GetAvgSalaryByDepartment
    @DeptId = 1,
    @AvgSalary = @Average OUTPUT;

SELECT @Average AS AverageSalary;
GO
```

### Результат

```text
-- EXEC sp_GetEmployeesByDepartment @DeptId = 1
EmployeeId EmployeeName      Salary   DepartmentId
---------- ----------------- -------  ------------
1          Анна Иванова      65000.00 1
2          Пётр Сидоров      72000.00 1
3          Мария Кузнецова   55000.00 1
4          Иван Петров       48000.00 1

-- OUTPUT-параметр
AverageSalary
-------------
60000.00
```

### Вывод

Обе процедуры работают: первая возвращает набор строк по отделу, вторая — одно значение через `OUTPUT`-параметр. `SET NOCOUNT ON` убирает лишние сообщения о количестве строк.

---

## Task 22 — Функции: скалярная и табличная

### SQL

```sql
CREATE FUNCTION fn_GetAnnualSalary
(
    @MonthlySalary DECIMAL(10,2)
)
RETURNS DECIMAL(12,2)
AS
BEGIN
    RETURN @MonthlySalary * 12;
END;
GO

CREATE FUNCTION fn_GetEmployeesBySalaryRange
(
    @Min DECIMAL(10,2),
    @Max DECIMAL(10,2)
)
RETURNS TABLE
AS
RETURN
(
    SELECT EmployeeId, EmployeeName, Salary, DepartmentId
    FROM Employees
    WHERE Salary BETWEEN @Min AND @Max
);
GO

SELECT
    EmployeeId,
    EmployeeName,
    Salary,
    dbo.fn_GetAnnualSalary(Salary) AS AnnualSalary
FROM Employees
ORDER BY EmployeeId;
GO

SELECT *
FROM dbo.fn_GetEmployeesBySalaryRange(50000, 70000)
ORDER BY Salary DESC;
GO
```

### Результат

```text
-- Скалярная функция в SELECT: 10 строк
EmployeeId EmployeeName      Salary   AnnualSalary
---------- ----------------- -------  ------------
1          Анна Иванова      65000.00 780000.00
2          Пётр Сидоров      72000.00 864000.00
3          Мария Кузнецова   55000.00 660000.00
4          Иван Петров       48000.00 576000.00
5          Ольга Смирнова    61000.00 732000.00
6          Дмитрий Орлов     58000.00 696000.00
7          Елена Волкова     52000.00 624000.00
8          Сергей Морозов    45000.00 540000.00
9          Наталья Соколова  43000.00 516000.00
10         Алексей Лебедев   90000.00 1080000.00

-- Табличная функция в FROM: диапазон 50000–70000
EmployeeId EmployeeName      Salary   DepartmentId
---------- ----------------- -------  ------------
1          Анна Иванова      65000.00 1
5          Ольга Смирнова    61000.00 2
6          Дмитрий Орлов     58000.00 2
3          Мария Кузнецова   55000.00 1
7          Елена Волкова     52000.00 2

(5 строк)
```

### Вывод

Скалярная функция вызывается в `SELECT` и возвращает годовой оклад. Табличная функция — inline TVF, поэтому вызывается в `FROM` как таблица и хорошо оптимизируется.

---

## Task 23 — Оконные функции: ROW_NUMBER, RANK, DENSE_RANK

### SQL

```sql
-- 23.1 Ранжирование на исходных данных
SELECT
    EmployeeId,
    EmployeeName,
    DepartmentId,
    Salary,
    ROW_NUMBER() OVER (ORDER BY Salary DESC, EmployeeId) AS RowNumber,
    RANK()       OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) AS RankInDepartment,
    DENSE_RANK() OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) AS DenseRankInDepartment
FROM Employees
ORDER BY DepartmentId, Salary DESC, EmployeeId;
GO

-- 23.2 Демонстрация равных зарплат: временно уравниваем Марию и Анну
BEGIN TRANSACTION;

UPDATE Employees
SET Salary = 65000
WHERE EmployeeId = 3;

SELECT
    EmployeeId,
    EmployeeName,
    DepartmentId,
    Salary,
    ROW_NUMBER() OVER (ORDER BY Salary DESC, EmployeeId) AS RowNumber,
    RANK()       OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) AS RankInDepartment,
    DENSE_RANK() OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) AS DenseRankInDepartment
FROM Employees
ORDER BY DepartmentId, Salary DESC, EmployeeId;

ROLLBACK TRANSACTION;
GO

-- 23.3 Проверка, что зарплата восстановлена
SELECT EmployeeId, EmployeeName, Salary
FROM Employees
WHERE EmployeeId = 3;
GO
```

### Результат

```text
-- 23.1 Исходные данные (все зарплаты уникальны)
EmployeeId EmployeeName      DepartmentId Salary   RowNumber RankInDepartment DenseRankInDepartment
---------- ----------------- ------------ -------  --------- ---------------- ---------------------
2          Пётр Сидоров      1            72000.00 2         1                1
1          Анна Иванова      1            65000.00 3         2                2
3          Мария Кузнецова   1            55000.00 6         3                3
4          Иван Петров       1            48000.00 8         4                4
5          Ольга Смирнова    2            61000.00 4         1                1
6          Дмитрий Орлов     2            58000.00 5         2                2
7          Елена Волкова     2            52000.00 7         3                3
8          Сергей Морозов    3            45000.00 9         1                1
9          Наталья Соколова  3            43000.00 10        2                2
10         Алексей Лебедев   4            90000.00 1         1                1

-- 23.2 Равные зарплаты: Анна и Мария по 65000.00
EmployeeId EmployeeName      DepartmentId Salary   RowNumber RankInDepartment DenseRankInDepartment
---------- ----------------- ------------ -------  --------- ---------------- ---------------------
2          Пётр Сидоров      1            72000.00 2         1                1
1          Анна Иванова      1            65000.00 3         2                2
3          Мария Кузнецова   1            65000.00 4         2                2
4          Иван Петров       1            48000.00 8         4                3
5          Ольга Смирнова    2            61000.00 5         1                1
6          Дмитрий Орлов     2            58000.00 6         2                2
7          Елена Волкова     2            52000.00 7         3                3
8          Сергей Морозов    3            45000.00 9         1                1
9          Наталья Соколова  3            43000.00 10        2                2
10         Алексей Лебедев   4            90000.00 1         1                1

-- 23.3 После ROLLBACK
EmployeeId EmployeeName      Salary
---------- ----------------- ------
3          Мария Кузнецова   55000.00
```

### Вывод

`ROW_NUMBER` всегда даёт уникальные номера. При равных зарплатах `RANK` пропускает следующее место (1, 2, 2, 4), а `DENSE_RANK` не пропускает (1, 2, 2, 3). На исходных данных зарплаты уникальны, поэтому разница не видна — для демонстрации использована временная транзакция с одинаковой зарплатой.

---

## Task 24 — Оконные агрегаты: SUM OVER, AVG OVER

### SQL

```sql
SELECT
    EmployeeId,
    EmployeeName,
    DepartmentId,
    Salary,
    SUM(Salary) OVER (PARTITION BY DepartmentId) AS DepartmentTotal,
    CAST(AVG(Salary) OVER (PARTITION BY DepartmentId) AS DECIMAL(10,2)) AS DepartmentAvg,
    CAST(Salary - AVG(Salary) OVER (PARTITION BY DepartmentId) AS DECIMAL(10,2)) AS Difference
FROM Employees
ORDER BY DepartmentId, EmployeeId;
GO

SELECT
    DepartmentId,
    SUM(Salary) AS DepartmentTotal,
    CAST(AVG(Salary) AS DECIMAL(10,2)) AS DepartmentAvg
FROM Employees
GROUP BY DepartmentId
ORDER BY DepartmentId;
GO
```

### Результат

```text
-- Оконные агрегаты: 10 строк
EmployeeId EmployeeName      DepartmentId Salary   DepartmentTotal DepartmentAvg Difference
---------- ----------------- ------------ -------  --------------- ------------- ----------
1          Анна Иванова      1            65000.00 240000.00       60000.00      5000.00
2          Пётр Сидоров      1            72000.00 240000.00       60000.00      12000.00
3          Мария Кузнецова   1            55000.00 240000.00       60000.00      -5000.00
4          Иван Петров       1            48000.00 240000.00       60000.00      -12000.00
5          Ольга Смирнова    2            61000.00 171000.00       57000.00      4000.00
6          Дмитрий Орлов     2            58000.00 171000.00       57000.00      1000.00
7          Елена Волкова     2            52000.00 171000.00       57000.00      -5000.00
8          Сергей Морозов    3            45000.00 88000.00        44000.00      1000.00
9          Наталья Соколова  3            43000.00 88000.00        44000.00      -1000.00
10         Алексей Лебедев   4            90000.00 90000.00        90000.00      0.00

-- Проверка через GROUP BY: 4 строки
DepartmentId DepartmentTotal DepartmentAvg
------------ --------------- -------------
1            240000.00       60000.00
2            171000.00       57000.00
3            88000.00        44000.00
4            90000.00        90000.00
```

### Вывод

Оконные суммы и средние по отделу совпадают с результатами `GROUP BY`. Отклонение `Salary - AVG(...) OVER (...)` считается для каждой строки без отдельного подзапроса.

---

## Task 25 — CTE (Common Table Expressions)

### SQL

```sql
-- 25.1 Переписывание задания 12 через CTE
WITH AvgSalary AS (
    SELECT AVG(Salary) AS AvgSalary
    FROM Employees
)
SELECT
    e.EmployeeId,
    e.EmployeeName,
    e.Salary
FROM Employees AS e
CROSS JOIN AvgSalary AS a
WHERE e.Salary > a.AvgSalary
ORDER BY e.Salary DESC;
GO

-- 25.2 Топ-3 отдела по средней зарплате
WITH DepartmentStats AS (
    SELECT
        DepartmentId,
        COUNT(*) AS EmployeeCount,
        AVG(Salary) AS AvgSalary
    FROM Employees
    GROUP BY DepartmentId
)
SELECT TOP (3)
    ds.DepartmentId,
    d.DepartmentName,
    ds.EmployeeCount,
    CAST(ds.AvgSalary AS DECIMAL(10,2)) AS AvgSalary
FROM DepartmentStats AS ds
INNER JOIN Departments AS d ON d.DepartmentId = ds.DepartmentId
ORDER BY ds.AvgSalary DESC;
GO
```

### Результат

```text
-- 25.1: те же 4 сотрудника, что и в задании 12
EmployeeId EmployeeName     Salary
---------- ---------------- ------
10         Алексей Лебедев  90000.00
2          Пётр Сидоров     72000.00
1          Анна Иванова     65000.00
5          Ольга Смирнова   61000.00

-- 25.2: топ-3 отдела
DepartmentId DepartmentName EmployeeCount AvgSalary
------------ -------------- ------------- ---------
4            Менеджмент     1             90000.00
1            Разработка     4             60000.00
2            Аналитика      3             57000.00
```

### Вывод

CTE-версия возвращает тот же результат, что и подзапрос в задании 12. Читаемость выше: сначала объявляется `AvgSalary`, затем основная выборка использует его как обычную таблицу. Во втором запросе CTE `DepartmentStats` упрощает топ-3 без вложенных агрегатов в `ORDER BY`.

---

## Task 26 — CASE: условная логика в SELECT

### SQL

```sql
SELECT
    EmployeeId,
    EmployeeName,
    Salary,
    CASE
        WHEN Salary > 60000 THEN 'High'
        WHEN Salary BETWEEN 40000 AND 60000 THEN 'Medium'
        ELSE 'Low'
    END AS SalaryLevel,
    HireDate,
    CASE
        WHEN HireDate < '2020-01-01' THEN 'Senior'
        WHEN HireDate < '2023-01-01' THEN 'Mid'
        ELSE 'Junior'
    END AS Tenure
FROM Employees
ORDER BY EmployeeId;
```

### Результат

```text
EmployeeId EmployeeName      Salary   SalaryLevel HireDate   Tenure
---------- ----------------- -------  ----------- ---------- ------
1          Анна Иванова      65000.00 High        2020-03-15 Mid
2          Пётр Сидоров      72000.00 High        2019-07-01 Senior
3          Мария Кузнецова   55000.00 Medium      2021-11-20 Mid
4          Иван Петров       48000.00 Medium      2023-02-10 Junior
5          Ольга Смирнова    61000.00 High        2020-09-05 Mid
6          Дмитрий Орлов     58000.00 Medium      2022-04-18 Mid
7          Елена Волкова     52000.00 Medium      2023-06-01 Junior
8          Сергей Морозов    45000.00 Medium      2021-12-12 Mid
9          Наталья Соколова  43000.00 Medium      2024-01-15 Junior
10         Алексей Лебедев   90000.00 High        2018-05-25 Senior
```

### Вывод

Все сотрудники получили корректные категории. `Low` не встретился, потому что минимальная зарплата в данных — 43000. `CASE` проверяет условия сверху вниз, поэтому диапазоны не пересекаются.

---

## Task 27 — Работа с датами: DATEADD, DATEDIFF, FORMAT

### SQL

```sql
SELECT
    EmployeeId,
    EmployeeName,
    HireDate,
    CAST(GETDATE() AS DATE) AS CurrentDate,
    DATEDIFF(YEAR, HireDate, GETDATE()) AS FullYears,
    DATEDIFF(MONTH, HireDate, GETDATE()) AS FullMonths,
    DATEADD(YEAR, 1, HireDate) AS OneYearLater,
    FORMAT(HireDate, 'dd.MM.yyyy') AS HireDateFormatted
FROM Employees
ORDER BY EmployeeId;
```

### Результат

```text
EmployeeId EmployeeName      HireDate   CurrentDate FullYears FullMonths OneYearLater HireDateFormatted
---------- ----------------- ---------- ----------- --------- ---------- ------------ -----------------
1          Анна Иванова      2020-03-15 2026-10-31  6         79         2021-03-15   15.03.2020
2          Пётр Сидоров      2019-07-01 2026-10-31  7         87         2020-07-01   01.07.2019
3          Мария Кузнецова   2021-11-20 2026-10-31  5         59         2022-11-20   20.11.2021
4          Иван Петров       2023-02-10 2026-10-31  3         44         2024-02-10   10.02.2023
5          Ольга Смирнова    2020-09-05 2026-10-31  6         73         2021-09-05   05.09.2020
6          Дмитрий Орлов     2022-04-18 2026-10-31  4         54         2023-04-18   18.04.2022
7          Елена Волкова     2023-06-01 2026-10-31  3         40         2024-06-01   01.06.2023
8          Сергей Морозов    2021-12-12 2026-10-31  5         58         2022-12-12   12.12.2021
9          Наталья Соколова  2024-01-15 2026-10-31  2         33         2025-01-15   15.01.2024
10         Алексей Лебедев   2018-05-25 2026-10-31  8         101        2019-05-25   25.05.2018
```

### Вывод

`DATEDIFF(MONTH, ...)` даёт количество месяцев, `DATEADD(YEAR, 1, ...)` — дату через год после найма, `FORMAT(..., 'dd.MM.yyyy')` — строку в нужном формате. Важно помнить: `DATEDIFF(YEAR, ...)` считает переходы через границу года, а не полные годы; в задании требуется именно такой вариант.

---

## Task 28 — C# + SQL: подключение и SELECT

### Код

Проект: [`src/PracticeDb.App`](../src/PracticeDb.App). Пакет `Microsoft.Data.SqlClient`, строка подключения берётся только из переменной окружения `PRACTICE_DB_CONNECTION`.

```csharp
var connectionString = Environment.GetEnvironmentVariable("PRACTICE_DB_CONNECTION");

await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

const string sql = """
    SELECT EmployeeId, EmployeeName, Salary
    FROM Employees
    ORDER BY EmployeeId;
    """;

await using var command = new SqlCommand(sql, connection);
await using var reader = await command.ExecuteReaderAsync();

while (await reader.ReadAsync())
{
    var employeeId = reader.GetInt32(0);
    var employeeName = reader.GetString(1);
    var employeeSalary = reader.GetDecimal(2);

    Console.WriteLine($"{employeeId,-4} {employeeName,-24} {employeeSalary,12:N2}");
}
```

Ошибки `SqlException` обрабатываются в верхнем уровне и в методе добавления; для распространённых номеров (547, 2627/2601, 4060, 18456) выводится понятное пояснение.

### Результат

```text
ID   Сотрудник                    Зарплата
--------------------------------------------
1    Анна Иванова                65,000.00
2    Пётр Сидоров                72,000.00
3    Мария Кузнецова             55,000.00
4    Иван Петров                 48,000.00
5    Ольга Смирнова              61,000.00
6    Дмитрий Орлов               58,000.00
7    Елена Волкова               52,000.00
8    Сергей Морозов              45,000.00
9    Наталья Соколова            43,000.00
10   Алексей Лебедев             90,000.00
```

### Вывод

Приложение подключается к `PracticeDB`, асинхронно открывает соединение и читает данные через `SqlDataReader`. Строка подключения не хранится в репозитории.

---

## Task 29 — C# + SQL: параметризованный INSERT

### Код

```csharp
const string insertSql = """
    INSERT INTO Employees (EmployeeName, Salary, HireDate, DepartmentId, ManagerId)
    OUTPUT INSERTED.EmployeeId
    VALUES (@name, @salary, @hireDate, @deptId, NULL);
    """;

await using var insertCommand = new SqlCommand(insertSql, connection);
insertCommand.Parameters.Add("@name", SqlDbType.NVarChar, 100).Value = name.Trim();

var salaryParameter = insertCommand.Parameters.Add("@salary", SqlDbType.Decimal);
salaryParameter.Precision = 10;
salaryParameter.Scale = 2;
salaryParameter.Value = salary;

insertCommand.Parameters.Add("@hireDate", SqlDbType.Date).Value = DateTime.Today;
insertCommand.Parameters.Add("@deptId", SqlDbType.Int).Value = departmentId;

var newEmployeeId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
```

После вставки выполняется `SELECT` по `EmployeeId`, чтобы показать добавленную запись. Для внешнего ключа обрабатывается `SqlException` с номером 547, для уникальности — 2627/2601.

### Результат

```text
> dotnet run -- add "Тестовый Сотрудник" 55000.00 2

Сотрудник добавлен. EmployeeId = 13.

ID   Сотрудник                    Зарплата   Дата найма  Отдел
------------------------------------------------------------------
13   Тестовый Сотрудник          55,000.00   31.10.2026      2
```

Проверка ошибки внешнего ключа:

```text
> dotnet run -- add "Ошибка Отдела" 40000.00 999

Не удалось добавить сотрудника: отдела с DepartmentId = 999 не существует
(нарушение внешнего ключа FK_Employees_Departments).
```

### Вывод

Параметры передаются через `SqlParameter`, конкатенации SQL-строк нет — это защищает от SQL-инъекций. Новый сотрудник появляется в базе, а ошибки FK и уникальности обрабатываются отдельно и не роняют приложение.

---

## Task 30 — C# + SQL: мини-отчёт по отделам

### Код

```csharp
const string sql = """
    SELECT
        d.DepartmentName,
        COUNT(e.EmployeeId) AS EmpCount,
        AVG(e.Salary) AS AvgSalary
    FROM Departments AS d
    LEFT JOIN Employees AS e ON d.DepartmentId = e.DepartmentId
    GROUP BY d.DepartmentName
    ORDER BY d.DepartmentName;
    """;

await using var reader = await command.ExecuteReaderAsync();

while (await reader.ReadAsync())
{
    var departmentName = reader.GetString(0);
    var employeeCount = reader.GetInt32(1);
    var averageSalary = reader.IsDBNull(2)
        ? "N/A"
        : reader.GetDecimal(2).ToString("N2", CultureInfo.CurrentCulture);

    Console.WriteLine($"{departmentName,-24} {employeeCount,12} {averageSalary,20}");
}
```

Отчёт выводится с выравниванием по колонкам. Для отдела без сотрудников `LEFT JOIN` даёт `NULL` в `AVG`, который заменяется на `N/A`; `COUNT(e.EmployeeId)` при этом возвращает `0`.

### Результат

Для демонстрации `NULL` временно добавлен отдел «Без сотрудников»:

```text
Отдел                     Сотрудников     Средняя зарплата
------------------------------------------------------------
Аналитика                           3            57,000.00
Без сотрудников                     0                  N/A
Менеджмент                          1            90,000.00
Разработка                          4            60,000.00
Тестирование                        2            44,000.00
------------------------------------------------------------
```

После демонстрации временный отдел удалён, база пересоздана через `init.sql`.

### Вывод

Отчёт корректно считает количество сотрудников и среднюю зарплату. Пустой отдел не пропадает благодаря `LEFT JOIN`, а `NULL` обрабатывается как `N/A`. Пустой результат также обрабатывается отдельной веткой `if (!reader.HasRows)`.

---

## Итоги

- Все 30 заданий выполнены.
- `init.sql` пересоздаёт `PracticeDB` с нуля одной командой.
- Задания 1–27 задокументированы в этом файле с SQL и фактическими результатами.
- Задания 28–30 реализованы в консольном приложении `src/PracticeDb.App`.
- Строка подключения хранится только в переменной окружения `PRACTICE_DB_CONNECTION`.
- Для заданий с `UPDATE`/`DELETE`/транзакциями показаны состояния до и после; после изменяющих заданий база пересоздавалась, чтобы дальнейшие результаты были воспроизводимы.

## Как проверить

```powershell
# 1. Пересоздать базу
sqlcmd -S .\SQLEXPRESS -E -C -i database\init.sql

# 2. Задать строку подключения только в текущей сессии
$env:PRACTICE_DB_CONNECTION = "Server=.\SQLEXPRESS;Database=PracticeDB;Integrated Security=True;TrustServerCertificate=True"

# 3. Собрать и запустить приложение
dotnet run --project src\PracticeDb.App -c Release
```

Без аргументов приложение открывает интерактивное меню. Для быстрой проверки:

```powershell
dotnet run --project src\PracticeDb.App -c Release -- select
dotnet run --project src\PracticeDb.App -c Release -- report
dotnet run --project src\PracticeDb.App -c Release -- add "Новый Сотрудник" 55000.00 2
```



