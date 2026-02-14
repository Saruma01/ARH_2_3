using System.Net.Sockets;
using System.Net;
using System.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

class Server
{
    private static TcpListener listener; // Объект для прослушивания входящих подключений
    private static List<TcpClient> clients = new List<TcpClient>(); // Список подключенных клиентов
    private static object lockObject = new object(); // Объект для синхронизации потоков
    private static string usersFile = "users.txt"; // Файл для хранения учетных данных пользователей

    static void Main()
    {
        int port = 5000; // Порт, на котором будет работать сервер
        listener = new TcpListener(IPAddress.Any, port); // Создаем слушателя на всех интерфейсах
        listener.Start(); // Запускаем слушателя
        Console.WriteLine($"Сервер запущен на порту {port}...");

        while (true) // Бесконечный цикл для принятия новых подключений
        {
            TcpClient client = listener.AcceptTcpClient(); // Принимаем новое подключение
            Thread thread = new Thread(() => HandleClient(client)); // Создаем поток для обработки клиента
            thread.Start(); // Запускаем поток
        }
    }

    private static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream(); // Получаем поток для обмена данными
        byte[] buffer = new byte[1024]; // Буфер для чтения данных
        int bytesRead; // Количество прочитанных байт

        // Аутентификация пользователя
        string username = AuthenticateUser(client);
        if (username == null) // Если аутентификация не удалась
        {
            client.Close(); // Закрываем соединение
            return;
        }

        // Добавляем клиента в список (с синхронизацией)
        lock (lockObject)
        {
            clients.Add(client);
        }

        Console.WriteLine($"{username} подключился к чату.");

        try
        {
            // Основной цикл чтения сообщений от клиента
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead); // Декодируем сообщение
                Console.WriteLine($"[{username}]: {message}"); // Логируем на сервере

                BroadcastMessage($"[{username}]: {message}", client); // Рассылаем всем клиентам
            }
        }
        catch
        {
            Console.WriteLine($"{username} отключился.");
        }
        finally
        {
            // Удаляем клиента из списка (с синхронизацией)
            lock (lockObject)
            {
                clients.Remove(client);
            }
            client.Close(); // Закрываем соединение
        }
    }

    private static string AuthenticateUser(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        while (true)
        {
            int bytesRead = stream.Read(buffer, 0, buffer.Length); // Читаем запрос от клиента
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead); // Декодируем запрос

            if (request.StartsWith("register:")) // Если запрос на регистрацию
            {
                string[] parts = request.Split(':'); // Разбиваем строку по разделителю
                string username = parts[1];
                string password = parts[2];

                if (RegisterUser(username, password)) // Пытаемся зарегистрировать
                {
                    SendMessage("success", stream); // Отправляем успех
                    return username; // Возвращаем имя пользователя
                }
                else
                {
                    SendMessage("error:Логин уже существует.", stream); // Отправляем ошибку
                }
            }
            else if (request.StartsWith("login:")) // Если запрос на вход
            {
                string[] parts = request.Split(':');
                string username = parts[1];
                string password = parts[2];

                if (ValidateUser(username, password)) // Проверяем учетные данные
                {
                    SendMessage("success", stream);
                    return username;
                }
                else
                {
                    SendMessage("error:Неверный логин или пароль.", stream);
                }
            }
        }
    }
    // Метод для регистрации нового пользователя
    private static bool RegisterUser(string username, string password)
    {
        lock (lockObject) // Блокируем доступ к файлу, чтобы избежать одновременной записи разными потоками
        {
            if (File.Exists(usersFile)) // Проверяем, существует ли файл с пользователями
            {
                string[] users = File.ReadAllLines(usersFile); // Читаем всех зарегистрированных пользователей
                foreach (string user in users)
                {
                    if (user.StartsWith(username + ":")) // Если логин уже существует, регистрация невозможна
                        return false;
                }
            }

            // Добавляем нового пользователя в конец файла
            File.AppendAllText(usersFile, $"{username}:{password}\n"); // Записываем логин и пароль в формате "логин:пароль"
            return true; // Регистрация успешна
        }
    }

    // Метод для проверки логина и пароля (авторизация)
    private static bool ValidateUser(string username, string password)
    {
        if (!File.Exists(usersFile)) // Если файла нет, значит, пользователей ещё нет
            return false;

        string[] users = File.ReadAllLines(usersFile); // Читаем все строки из файла (каждая строка — "логин:пароль")
        foreach (string user in users)
        {
            string[] parts = user.Split(':'); // Разделяем строку по символу :, получаем массив из [логин, пароль]
            if (parts[0] == username && parts[1] == password) // Если логин и пароль совпадают, авторизация успешна
                return true;
        }

        return false; // Если совпадений нет, авторизация не удалась
    }

    private static void SendMessage(string message, NetworkStream stream)
    {
        byte[] data = Encoding.UTF8.GetBytes(message); // Кодируем сообщение в байты
        stream.Write(data, 0, data.Length); // Отправляем
    }

    private static void BroadcastMessage(string message, TcpClient sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(message); // Кодируем сообщение
        lock (lockObject) // Синхронизация доступа к списку клиентов
        {
            foreach (var client in clients)
            {
                if (client != sender) // Не отправляем сообщение отправителю
                {
                    try
                    {
                        client.GetStream().Write(data, 0, data.Length); // Отправляем
                    }
                    catch
                    {
                        client.Close(); // В случае ошибки закрываем соединение
                    }
                }
            }
        }
    }
}