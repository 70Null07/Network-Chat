using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Client
{
    public sealed partial class ClientApplication : Page
    {
        // Порт для удаленного устройства  
        private const int port = 11000;

        // ManualResetEvent instances signal completion.  
        private static readonly ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static readonly ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static readonly ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        // Строка для ответа от удаленного устройства
        private static string response = default;
        private readonly IPAddress ipAddress = IPAddress.Parse("217.144.172.106");
        private IPEndPoint remoteEP;
        private Socket client;

        // Таймер автообновления сообщений
        private static System.Timers.Timer aTimer;

        public ClientApplication()
        {
            try
            {
                InitializeComponent();

                remoteEP = new IPEndPoint(ipAddress, port);

                // Создание TCP/IP сокета 
                client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp); ;

                client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();

                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                object value = localSettings.Values["userId"];
                string sendingString = "RequestContactList," + value.ToString() + ",<EOF>";

                // Отпрвка команды на удаленное устройство  
                Send(client, sendingString);
                sendDone.WaitOne();

                Thread.Sleep(300);

                // Получение ответа от удаленно устройтсва 
                Receive(client);
                receiveDone.WaitOne();

                string[] addConatacts = response.Split(",");

                for (int i = 0; i < addConatacts.Length - 1; i += 3)
                {
                    addContact(int.Parse(addConatacts[i]), addConatacts[i + 1].Replace(" ", "") + " " + addConatacts[i + 2].Replace(" ", ""));
                }

                messageInput.IsEnabled = false;

                aTimer = new System.Timers.Timer(5000);

                aTimer.Elapsed += ATimer_Elapsed;
                aTimer.AutoReset = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async void ATimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                string Name = "";
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    Grid listView = contactsList.SelectedItem as Grid;
                    Name = listView.Name;
                });

                if (Name != null)
                {
                    remoteEP = new IPEndPoint(ipAddress, port);  
                    Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                    connectDone.WaitOne();

                    Thread.Sleep(300);

                    Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    object value = localSettings.Values["userId"];

                    string sendingString = "RequestMessage," + value.ToString() + "," + Name + ",<EOF>";
 
                    Send(client, sendingString);
                    sendDone.WaitOne();

                    Thread.Sleep(300);
 
                    Receive(client);
                    receiveDone.WaitOne();

                    string[] addMessages = response.Split(",");

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        messageList.Children.Clear();
                        for (int i = 0; i < addMessages.Length - 1; i += 3)
                        {
                            if (addMessages[i].Replace(" ", "") == value.ToString())
                            {
                                addToMessage(addMessages[i + 2].ToString());
                            }
                            else
                            {
                                addFromMessage(addMessages[i + 2].ToString());
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void Receive(Socket client)
        {
            try
            {
                // Create the state object.  
                StateObject state = new StateObject
                {
                    workSocket = client
                };

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
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
                    state.sb.Append(Encoding.UTF8.GetString(state.buffer, 0, bytesRead));

                    // Get the rest of the data.  
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    response = state.sb.ToString();

                    // Signal that all bytes have been received.  
                    receiveDone.Set();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void Send(Socket client, string data)
        {
            try
            {
                // Convert the string data to byte data using ASCII encoding.  
                byte[] byteData = Encoding.UTF8.GetBytes(data);

                // Begin sending the data to the remote device.  
                client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);

                // Signal that all bytes have been sent.  
                sendDone.Set();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void addContact(int contactId, string contact)
        {
            try
            {
                Grid grid = new Grid()
                {
                    Name = contactId.ToString(),
                    Height = 75,
                    Margin = new Thickness(0, 10, 0, 10),
                };

                SolidColorBrush borderColor = new SolidColorBrush()
                {
                    Color = Color.FromArgb(128, 187, 198, 129),
                };

                SolidColorBrush fillColor = new SolidColorBrush()
                {
                    Color = Color.FromArgb(255, 78, 100, 54),
                };

                Rectangle rectangle = new Rectangle()
                {
                    StrokeDashArray = new DoubleCollection() { 6, 1 },
                    Stroke = borderColor,
                    StrokeThickness = 5,
                    Fill = fillColor,
                    Margin = new Thickness(-10, 0, -10, 0),
                };
                rectangle.Width = contactsList.Width;

                TextBlock textBlock = new TextBlock()
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Text = contact,
                    FontSize = 35,
                    Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                };
                grid.Children.Add(rectangle);
                grid.Children.Add(textBlock);
                contactsList.Items.Add(grid);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void addFromMessage(string text)
        {
            try
            {
                Grid grid = new Grid()
                {
                    Height = 60,
                    Margin = new Thickness(15, 15, 15, 15),
                };

                Rectangle rectangle = new Rectangle()
                {
                    Fill = new SolidColorBrush(Color.FromArgb(255, 50, 75, 53)),
                };

                TextBlock textBlock = new TextBlock()
                {
                    Margin = new Thickness(25, 10, 0, 0),
                    FontSize = 30,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 187, 198, 129)),
                    Text = text,
                };

                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(6, GridUnitType.Star), });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(4, GridUnitType.Star), });

                grid.Children.Add(rectangle);
                grid.Children.Add(textBlock);

                rectangle.SetValue(Grid.ColumnProperty, 0);
                textBlock.SetValue(Grid.ColumnProperty, 0);

                messageList.Children.Add(grid);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void addToMessage(string text)
        {
            try
            {
                Grid grid = new Grid()
                {
                    Height = 60,
                    Margin = new Thickness(15, 10, 15, 15),
                };

                Rectangle rightRectangle = new Rectangle()
                {
                    Fill = new SolidColorBrush(Color.FromArgb(255, 92, 116, 94)),
                };

                Rectangle leftRectangle = new Rectangle()
                {
                    Fill = new SolidColorBrush(Color.FromArgb(255, 142, 167, 99)),
                };

                TextBlock textBlock = new TextBlock()
                {
                    Margin = new Thickness(15, 10, 0, 0),
                    FontSize = 30,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                    Text = text,
                };

                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(16, GridUnitType.Star), });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(16, GridUnitType.Star), });

                grid.Children.Add(rightRectangle);
                grid.Children.Add(leftRectangle);
                grid.Children.Add(textBlock);

                leftRectangle.SetValue(Grid.ColumnProperty, 1);
                rightRectangle.SetValue(Grid.ColumnProperty, 2);
                textBlock.SetValue(Grid.ColumnProperty, 2);

                messageList.Children.Add(grid);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void contactsList_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                messageList.Children.Clear();

                Grid listView = contactsList.SelectedItem as Grid;

                if (listView != null)
                {
                    remoteEP = new IPEndPoint(ipAddress, port);

                    client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                    connectDone.WaitOne();

                    Thread.Sleep(300);

                    Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    object value = localSettings.Values["userId"];

                    string sendingString = "RequestMessage," + value.ToString() + "," + listView.Name + ",<EOF>";
 
                    Send(client, sendingString);
                    sendDone.WaitOne();

                    Thread.Sleep(300);

                    Receive(client);
                    receiveDone.WaitOne();

                    string[] addMessages = response.Split(",");

                    for (int i = 0; i < addMessages.Length - 1; i += 3)
                    {
                        if (addMessages[i].Replace(" ", "") == value.ToString())
                        {
                            addToMessage(addMessages[i + 2].ToString());
                        }
                        else
                        {
                            addFromMessage(addMessages[i + 2].ToString());
                        }
                    }
                    messageInput.IsEnabled = true;

                    aTimer.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void messageInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                if (e.Key == Windows.System.VirtualKey.Enter && messageInput.Text != string.Empty)
                {
                    addToMessage(messageInput.Text);

                    Grid listView = contactsList.SelectedItem as Grid;

                    remoteEP = new IPEndPoint(ipAddress, port);
                    client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp); ;

                    client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                    connectDone.WaitOne();

                    Thread.Sleep(300);

                    Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    object value = localSettings.Values["userId"];

                    string sendingString = "SendMessage," + value.ToString() + "," + listView.Name + "," + messageInput.Text + ",<EOF>";

                    Send(client, sendingString);
                    sendDone.WaitOne();

                    Thread.Sleep(300);
 
                    Receive(client);
                    receiveDone.WaitOne();

                    messageInput.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async void addContactButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddContactCustomDialog addContactDialog = new AddContactCustomDialog();
                await addContactDialog.ShowAsync();

                remoteEP = new IPEndPoint(ipAddress, port);

                Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
  
                client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();

                Thread.Sleep(300);

                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                object value = localSettings.Values["userId"];

                string[] userInfo = addContactDialog.addContactUserName.Text.Split(" ");

                string sendingString = "AddContact," + value.ToString() + "," + userInfo[0] + "," + userInfo[1] + ",<EOF>";
 
                Send(client, sendingString);
                sendDone.WaitOne();

                Thread.Sleep(300);

                Receive(client);
                receiveDone.WaitOne();

                string[] addNewContacts = response.Split(",");
                if (addNewContacts.Length > 2)
                {
                    addContact(int.Parse(addNewContacts[0]), addNewContacts[1].Replace(" ", "") + " " + addNewContacts[2].Replace(" ", ""));
                }
                else
                {
                    ContentDialog contentDialog = new ContentDialog()
                    {
                        Content = "Указанный пользователь не существует",
                        PrimaryButtonText = "Отмена",
                    };
                    await contentDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
