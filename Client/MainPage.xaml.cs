using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Client
{

    public class StateObject
    {
        // Client socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 256;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }

    public partial class MainPage : Page
    {
        // The port number for the remote device.  
        private const int port = 11000;
        private bool result;

        // ManualResetEvent instances signal completion.  
        private static readonly ManualResetEvent connectDone = new ManualResetEvent(false);
        private static readonly ManualResetEvent sendDone = new ManualResetEvent(false);
        private static readonly ManualResetEvent receiveDone = new ManualResetEvent(false);

        // The response from the remote device.  
        private static string response = default;
        private readonly IPAddress ipAddress = IPAddress.Parse("217.144.172.106");
        private IPEndPoint remoteEP;
        private Socket client;

        public MainPage()
        {
            InitializeComponent();

            Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplicationView.GetForCurrentView().Title = "Zipper";
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            Window.Current.SetTitleBar(UserLayout);
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            remoteEP = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.  
            client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Connect to the remote endpoint.  
            client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
            connectDone.WaitOne();

            string sendauthdata = "Authorization," + LoginBox.Text + "," + PasswordBox.Text + ",<EOF>";

            Thread.Sleep(15);

            // Send test data to the remote device.  
            Send(client, sendauthdata);
            sendDone.WaitOne();

            Thread.Sleep(15);

            // Receive the response from the remote device.  
            Receive(client);
            receiveDone.WaitOne();

            if (result)
            {
                Frame.Navigate(typeof(ClientApplication));
            }
            else
            {
                DisplayErrorAuthorization();
            };
        }

        private async void DisplayErrorAuthorization()
        {
            ContentDialog displayErrorAuthorization = new ContentDialog
            {
                Title = "Произошла ошибка при авторизации",
                Content = "Неправильный логин или пароль.",
                CloseButtonText = "Ok"
            };

            await displayErrorAuthorization.ShowAsync();
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);

                // Signal that the connection has been made.  
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Receive(Socket client)
        {
            // Create the state object.  
            StateObject state = new StateObject
            {
                workSocket = client
            };

            // Begin receiving the data from the remote device.
            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {

            // Retrieve the state object and the client socket
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket client = state.workSocket;

            // Read data from the remote device.  
            int bytesRead = client.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There might be more data, so store the data received so far.  
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // Get the rest of the data.  
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            else
            {
                string[] responseString = state.sb.ToString().Split(",");

                response = responseString[0];
                if (responseString.Length > 1)
                {
                    string userId = responseString[1];
                    Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["userId"] = responseString[1];
                }

                if (response == "true")
                {
                    result = true;
                }
                else
                {
                    result = false;
                }

                // Signal that all bytes have been received.  
                receiveDone.Set();
            }
        }

        private void Send(Socket client, string data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            // Retrieve the socket from the state object.  
            Socket client = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = client.EndSend(ar);

            // Signal that all bytes have been sent.  
            sendDone.Set();
        }

        private async void userRegistration(object sender, RoutedEventArgs e)
        {
            NewUserCustomDialog newUser = new NewUserCustomDialog();
            await newUser.ShowAsync();

            remoteEP = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.  
            client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Connect to the remote endpoint.  
            client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
            connectDone.WaitOne();

            Thread.Sleep(5);

            string sendauthdata = "Registration," + newUser.LoginBox.Text + "," + newUser.PasswordBox.Text + "," + newUser.UserNameBox.Text.Replace(" ", ",") + ",<EOF>";
            Send(client, sendauthdata);
            sendDone.WaitOne();

            Thread.Sleep(5);
            // Receive the response from the remote device.  

            Receive(client);
            receiveDone.WaitOne();

            if (result)
            {
                ContentDialog displayErrorAuthorization = new ContentDialog
                {
                    Title = "Успешная регистрация",
                    Content = "Пользователь успешно зарегистрирован.",
                    CloseButtonText = "Ok"
                };

                await displayErrorAuthorization.ShowAsync();
            }
            else
            {
                ContentDialog displayErrorAuthorization = new ContentDialog
                {
                    Title = "Произошла ошибка при регистрации",
                    Content = "Такой пользователь уже существует.",
                    CloseButtonText = "Ok"
                };

                await displayErrorAuthorization.ShowAsync();
            }
        }

        private void SymbolIcon_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }
    }
}
