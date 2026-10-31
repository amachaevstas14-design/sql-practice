using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

const string ConnectionStringVariable = "PRACTICE_DB_CONNECTION";

Console.OutputEncoding = Encoding.UTF8;

var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"Не задана переменная окружения {ConnectionStringVariable}.");
    Console.Error.WriteLine("Пример для SQL Server Express:");
    Console.Error.WriteLine("  $env:PRACTICE_DB_CONNECTION = \"Server=.\\SQLEXPRESS;Database=PracticeDB;Integrated Security=True;TrustServerCertificate=True\"");
    return 1;
}

try
{
    if (args.Length > 0)
    {
        await RunCommandAsync(args);
    }
    else
    {
        await RunMenuAsync();
    }

    return 0;
}
catch (SqlException exception)
{
    PrintSqlException(exception);
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Непредвиденная ошибка: {exception.Message}");
    return 1;
}

async Task RunMenuAsync()
{
    Console.WriteLine("==============================================");
    Console.WriteLine(" PracticeDB — практикум по SQL Server");
    Console.WriteLine(" Задания 28–30: интеграция с C#");
    Console.WriteLine("==============================================");

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("1 — показать сотрудников");
        Console.WriteLine("2 — добавить сотрудника");
        Console.WriteLine("3 — отчёт по отделам");
        Console.WriteLine("0 — выход");
        Console.Write("Выберите пункт: ");

        var choice = Console.ReadLine()?.Trim();

        switch (choice)
        {
            case "1":
                await ListEmployeesAsync();
                break;
            case "2":
                await PromptAndAddEmployeeAsync();
                break;
            case "3":
                await PrintDepartmentReportAsync();
                break;
            case "0":
            case null:
                return;
            default:
                Console.WriteLine("Неизвестный пункт меню. Введите 0, 1, 2 или 3.");
                break;
        }
    }
}

async Task RunCommandAsync(string[] commandLineArgs)
{
    var command = commandLineArgs[0].ToLowerInvariant();

    switch (command)
    {
        case "select":
        case "1":
            await ListEmployeesAsync();
            break;

        case "add":
        case "2":
            if (commandLineArgs.Length < 4)
            {
                PrintUsage();
                return;
            }

            var name = commandLineArgs[1];

            if (!decimal.TryParse(commandLineArgs[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var salary))
            {
                Console.Error.WriteLine("Зарплата должна быть числом, например 55000.00.");
                return;
            }

            if (!int.TryParse(commandLineArgs[3], out var departmentId))
            {
                Console.Error.WriteLine("DepartmentId должен быть целым числом.");
                return;
            }

            await AddEmployeeAsync(name, salary, departmentId);
            break;

        case "report":
        case "3":
            await PrintDepartmentReportAsync();
            break;

        default:
            PrintUsage();
            break;
    }
}

async Task ListEmployeesAsync()
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    const string sql = """
        SELECT EmployeeId, EmployeeName, Salary
        FROM Employees
        ORDER BY EmployeeId;
        """;

    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    if (!reader.HasRows)
    {
        Console.WriteLine("Сотрудники не найдены.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"{"ID",-4} {"Сотрудник",-24} {"Зарплата",12}");
    Console.WriteLine(new string('-', 44));

    while (await reader.ReadAsync())
    {
        var employeeId = reader.GetInt32(0);
        var employeeName = reader.GetString(1);
        var employeeSalary = reader.GetDecimal(2);

        Console.WriteLine($"{employeeId,-4} {employeeName,-24} {employeeSalary,12:N2}");
    }
}

async Task AddEmployeeAsync(string name, decimal salary, int departmentId)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        Console.Error.WriteLine("Имя сотрудника не может быть пустым.");
        return;
    }

    if (salary < 0)
    {
        Console.Error.WriteLine("Зарплата не может быть отрицательной.");
        return;
    }

    if (departmentId <= 0)
    {
        Console.Error.WriteLine("DepartmentId должен быть положительным числом.");
        return;
    }

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    const string insertSql = """
        INSERT INTO Employees (EmployeeName, Salary, HireDate, DepartmentId, ManagerId)
        OUTPUT INSERTED.EmployeeId
        VALUES (@name, @salary, @hireDate, @deptId, NULL);
        """;

    try
    {
        await using var insertCommand = new SqlCommand(insertSql, connection);
        insertCommand.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 100).Value = name.Trim();

        var salaryParameter = insertCommand.Parameters.Add("@salary", System.Data.SqlDbType.Decimal);
        salaryParameter.Precision = 10;
        salaryParameter.Scale = 2;
        salaryParameter.Value = salary;

        insertCommand.Parameters.Add("@hireDate", System.Data.SqlDbType.Date).Value = DateTime.Today;
        insertCommand.Parameters.Add("@deptId", System.Data.SqlDbType.Int).Value = departmentId;

        var newEmployeeId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());

        Console.WriteLine($"Сотрудник добавлен. EmployeeId = {newEmployeeId}.");

        const string selectSql = """
            SELECT EmployeeId, EmployeeName, Salary, HireDate, DepartmentId
            FROM Employees
            WHERE EmployeeId = @employeeId;
            """;

        await using var selectCommand = new SqlCommand(selectSql, connection);
        selectCommand.Parameters.Add("@employeeId", System.Data.SqlDbType.Int).Value = newEmployeeId;

        await using var reader = await selectCommand.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            Console.WriteLine();
            Console.WriteLine($"{"ID",-4} {"Сотрудник",-24} {"Зарплата",12} {"Дата найма",12} {"Отдел",6}");
            Console.WriteLine(new string('-', 66));
            Console.WriteLine(
                $"{reader.GetInt32(0),-4} {reader.GetString(1),-24} {reader.GetDecimal(2),12:N2} " +
                $"{reader.GetDateTime(3),12:dd.MM.yyyy} {reader.GetInt32(4),6}");
        }
    }
    catch (SqlException exception) when (exception.Number == 547)
    {
        Console.Error.WriteLine(
            $"Не удалось добавить сотрудника: отдела с DepartmentId = {departmentId} не существует " +
            "(нарушение внешнего ключа FK_Employees_Departments).");
    }
    catch (SqlException exception) when (exception.Number is 2627 or 2601)
    {
        Console.Error.WriteLine($"Не удалось добавить сотрудника: нарушено ограничение уникальности. {exception.Message}");
    }
}

async Task PromptAndAddEmployeeAsync()
{
    Console.Write("Имя сотрудника: ");
    var name = Console.ReadLine() ?? string.Empty;

    Console.Write("Зарплата: ");
    var salaryText = Console.ReadLine();

    if (!decimal.TryParse(salaryText, NumberStyles.Number, CultureInfo.CurrentCulture, out var salary))
    {
        Console.WriteLine("Зарплата должна быть числом, например 55000,00.");
        return;
    }

    Console.Write("DepartmentId: ");
    var departmentText = Console.ReadLine();

    if (!int.TryParse(departmentText, out var departmentId))
    {
        Console.WriteLine("DepartmentId должен быть целым числом.");
        return;
    }

    await AddEmployeeAsync(name, salary, departmentId);
}

async Task PrintDepartmentReportAsync()
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

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

    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    if (!reader.HasRows)
    {
        Console.WriteLine("Нет данных для отчёта.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"{"Отдел",-24} {"Сотрудников",12} {"Средняя зарплата",20}");
    Console.WriteLine(new string('-', 60));

    while (await reader.ReadAsync())
    {
        var departmentName = reader.GetString(0);
        var employeeCount = reader.GetInt32(1);
        var averageSalary = reader.IsDBNull(2)
            ? "N/A"
            : reader.GetDecimal(2).ToString("N2", CultureInfo.CurrentCulture);

        Console.WriteLine($"{departmentName,-24} {employeeCount,12} {averageSalary,20}");
    }

    Console.WriteLine(new string('-', 60));
}

void PrintUsage()
{
    Console.WriteLine("Использование:");
    Console.WriteLine("  dotnet run -- select");
    Console.WriteLine("  dotnet run -- add \"Имя сотрудника\" 55000.00 2");
    Console.WriteLine("  dotnet run -- report");
    Console.WriteLine("Без аргументов приложение запускает интерактивное меню.");
}

void PrintSqlException(SqlException exception)
{
    Console.Error.WriteLine($"Ошибка SQL Server (номер {exception.Number}): {exception.Message}");

    switch (exception.Number)
    {
        case 547:
            Console.Error.WriteLine("Причина: нарушение внешнего ключа — проверьте DepartmentId/ManagerId.");
            break;
        case 2627:
        case 2601:
            Console.Error.WriteLine("Причина: нарушение уникальности — такая запись уже существует.");
            break;
        case 4060:
            Console.Error.WriteLine("Причина: не удалось открыть базу PracticeDB — проверьте строку подключения.");
            break;
        case 18456:
            Console.Error.WriteLine("Причина: ошибка входа — проверьте пользователя и параметры аутентификации.");
            break;
    }
}
