using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Client
{
    static void Main()
    {
        string serverIP = "127.0.0.1"; // IP-адрес сервера (localhost)
        int port = 5000; // Порт сервера

        try
        {
            // Создаем TCP-клиент и подключаемся к серверу
            TcpClient client = new TcpClient(serverIP, port);
            NetworkStream stream = client.GetStream(); // Получаем поток для обмена данными

            // Проходим аутентификацию
            if (!Authenticate(client))
            {
                client.Close(); // Закрываем соединение при неудачной аутентификации
                return;
            }

            Console.WriteLine("Подключено к серверу. Введите сообщения:");

            // Создаем и запускаем поток для получения сообщений
            Thread receiveThread = new Thread(() => ReceiveMessages(client));
            receiveThread.Start();

            // Основной цикл отправки сообщений
            while (true)
            {
                string message = Console.ReadLine(); // Читаем сообщение с консоли
                byte[] data = Encoding.UTF8.GetBytes(message); // Кодируем в байты
                stream.Write(data, 0, data.Length); // Отправляем на сервер
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}"); // Обработка ошибок подключения
        }
    }

    private static bool Authenticate(TcpClient client)
    {
        NetworkStream stream = client.GetStream(); // Поток для обмена данными
        byte[] buffer = new byte[1024]; // Буфер для чтения ответов

        while (true) // Цикл повторяется до успешной аутентификации
        {
            Console.WriteLine("Выберите действие: 1 - Вход, 2 - Регистрация");
            string choice = Console.ReadLine(); // Получаем выбор пользователя

            if (choice == "1") // Вход
            {
                Console.Write("Логин: ");
                string username = Console.ReadLine();
                Console.Write("Пароль: ");
                string password = Console.ReadLine();
                // Отправляем запрос на вход
                SendMessage($"login:{username}:{password}", stream);
            }
            else if (choice == "2") // Регистрация
            {
                Console.Write("Придумайте логин: ");
                string username = Console.ReadLine();
                Console.Write("Придумайте пароль: ");
                string password = Console.ReadLine();
                // Отправляем запрос на регистрацию
                SendMessage($"register:{username}:{password}", stream);
            }
            else
            {
                Console.WriteLine("Неверный ввод. Повторите попытку.");
                continue; // Повторяем цикл при неверном выборе
            }

            // Читаем ответ сервера
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (response == "success") // Успешная аутентификация
            {
                Console.WriteLine("Вы успешно вошли в чат!");
                return true;
            }
            else if (response.StartsWith("error:")) // Ошибка
            {
                Console.WriteLine(response.Substring(6)); // Выводим текст ошибки (после "error:")
            }
        }
    }

    private static void SendMessage(string message, NetworkStream stream)
    {
        byte[] data = Encoding.UTF8.GetBytes(message); // Кодируем сообщение в байты
        stream.Write(data, 0, data.Length); // Отправляем данные
    }

    private static void ReceiveMessages(TcpClient client)
    {
        NetworkStream stream = client.GetStream(); // Поток для обмена данными
        byte[] buffer = new byte[1024]; // Буфер для чтения данных
        int bytesRead; // Количество прочитанных байт

        try
        {
            // Бесконечный цикл чтения сообщений
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead); // Декодируем сообщение
                Console.WriteLine(message); // Выводим сообщение
            }
        }
        catch
        {
            Console.WriteLine("Отключено от сервера."); // Обработка разрыва соединения
        }
    }
}