using System;
using System.Data.SqlClient;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// State object for reading client data asynchronously  
public class StateObject
{
    // Размер буфера  
    public const int BufferSize = 1024;

    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];

    // Received data string.
    public StringBuilder sb = new StringBuilder();

    // Client socket.
    public Socket? workSocket = null;
}

public class AsynchronousSocketListener
{
    private static SqlConnection? connection;

    // Thread signal.  
    public static ManualResetEvent allDone = new ManualResetEvent(false);

    public AsynchronousSocketListener()
    {
    }

    public static void StartListening()
    {
        // Establish the local endpoint for the socket.  
        // The DNS name of the computer  
        // running the listener is "host.contoso.com".  
        //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        //IPAddress ipAddress = ipHostInfo.AddressList[0];
        IPAddress ipAddress = IPAddress.Parse("192.168.0.105");
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

        Console.WriteLine(ipAddress.AddressFamily);

        // Create a TCP/IP socket.  
        Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.  
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                // Set the event to nonsignaled state.  
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.  
                Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                // Wait until a connection is made before continuing.  
                allDone.WaitOne();
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.  
        allDone.Set();

        // Get the socket that handles the client request.  
        Socket? listener = ar.AsyncState as Socket;
        Socket handler = listener.EndAccept(ar);

        // Create the state object.  
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
    }

    public static void ReadCallback(IAsyncResult ar)
    {
        try
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.  
                state.sb.Append(Encoding.UTF8.GetString(
                    state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read
                // more data.  
                content = state.sb.ToString();
                if (content.IndexOf("<EOF>") > -1)
                {
                    // All the data has been read from the
                    // client. Display it on the console.  
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);

                    string[] data = content.Split(",");

                    switch (data[0])
                    {
                        case "Authorization":
                            {
                                string command = "SELECT * FROM AuthTBL WHERE AuthName LIKE '" + data[1] + "%' AND AuthPassword LIKE '" + data[2] + "%'";
                                var myCommand = new SqlCommand(command, connection);
                                SqlDataReader sqlDataReader = myCommand.ExecuteReader();

                                content = String.Empty;

                                if (sqlDataReader.HasRows)
                                {
                                    sqlDataReader.Read();
                                    content = "true" + "," + sqlDataReader.GetInt32(0);
                                }
                                else
                                    content = "false";
                                sqlDataReader.Close();
                            }
                            break;
                        case "RequestContactList":
                            {
                                string command = "SELECT * FROM ContactsTBL WHERE UserId LIKE '" + data[1] + "%'";
                                var myCommand = new SqlCommand(command, connection);
                                SqlDataReader sqlDataReader = myCommand.ExecuteReader();

                                content = String.Empty;

                                while (sqlDataReader.Read())
                                {
                                    string command1 = "SELECT * FROM AuthTBL WHERE Id LIKE '" + sqlDataReader.GetInt32(2) + "%'";
                                    var myCommand1 = new SqlCommand(command1, connection);
                                    SqlDataReader sqlDataReader1 = myCommand1.ExecuteReader();
                                    sqlDataReader1.Read();
                                    content += sqlDataReader1.GetInt32(0) + "," + sqlDataReader1.GetString(3) + "," + sqlDataReader1.GetString(4) + ",";
                                }
                            }
                            break;
                        case "RequestMessage":
                            {
                                string command = "SELECT * FROM MessagesTBL WHERE UserFrom LIKE '" + data[1] + "%' AND UserTo LIKE '" + data[2] + "%' OR UserFrom LIKE '" + data[2] + "%' AND UserTo LIKE '" + data[1] + "%'";
                                var myCommand = new SqlCommand(command, connection);
                                SqlDataReader sqlDataReader = myCommand.ExecuteReader();

                                content = String.Empty;

                                while (sqlDataReader.Read())
                                {
                                    content += sqlDataReader.GetInt32(1) + "," + sqlDataReader.GetInt32(2) + "," + sqlDataReader.GetString(3) + ",";
                                }
                            }
                            break;
                        case "SendMessage":
                            {
                                string command = "INSERT INTO MessagesTBL (UserFrom,UserTo,Message) VALUES ('" + data[1] + "', '" + data[2] + "', '" + data[3] + "')";
                                var myCommand = new SqlCommand(command, connection);
                                SqlDataReader sqlDataReader = myCommand.ExecuteReader();
                            }
                            break;
                        case "Registration":
                            {
                                string command = "SELECT * FROM AuthTBL WHERE AuthName LIKE '" + data[1] + "%' AND AuthPassword LIKE '" + data[2] + "%'";
                                var myCommand = new SqlCommand(command, connection);
                                SqlDataReader sqlDataReader = myCommand.ExecuteReader();

                                content = String.Empty;

                                if (sqlDataReader.HasRows)
                                {
                                    content = "false";
                                }
                                else
                                {
                                    string command1 = "INSERT INTO AuthTBL (AuthName,AuthPassword,RealName,RealSurname) VALUES ('" + data[1] + "', '" + data[2] + "', '" + data[3] + "', '" + data[4] + "')";
                                    var myCommand1 = new SqlCommand(command1, connection);
                                    SqlDataReader sqlDataReader1 = myCommand1.ExecuteReader();

                                    content = "true";
                                }
                                sqlDataReader.Close();
                            }
                            break;
                        case "AddContact":
                            {
                                string command = "SELECT * FROM AuthTBL WHERE RealName LIKE '" + data[2] + "%' AND RealSurname LIKE '" + data[3] + "%'";
                                var myCommand = new SqlCommand(command, connection);
                                SqlDataReader sqlDataReader = myCommand.ExecuteReader();

                                content = String.Empty;

                                if (sqlDataReader.HasRows)
                                {
                                    sqlDataReader.Read();

                                    content += sqlDataReader.GetInt32(0) + "," + sqlDataReader.GetString(3) + "," + sqlDataReader.GetString(4);

                                    string command1 = "INSERT INTO ContactsTBL (UserId,FriendID) VALUES ('" + int.Parse(data[1]) + "', '" + sqlDataReader.GetInt32(0) + "')";
                                    var myCommand1 = new SqlCommand(command1, connection);
                                    SqlDataReader sqlDataReader1 = myCommand1.ExecuteReader();

                                    string command2 = "INSERT INTO ContactsTBL (UserId,FriendID) VALUES ('" + sqlDataReader.GetInt32(0) + "', '" + int.Parse(data[1]) + "')";
                                    var myCommand2 = new SqlCommand(command2, connection);
                                    SqlDataReader sqlDataReader2 = myCommand2.ExecuteReader();
                                }
                                else
                                {
                                    content += "false";
                                }
                            }
                            break;
                    }

                    Send(handler, content);
                }
                else
                {
                    // Not all data received. Get more.  
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private static void Send(Socket handler, String data)
    {
        // Convert the string data to byte data using ASCII encoding.  
        byte[] byteData = Encoding.UTF8.GetBytes(data);

        // Begin sending the data to the remote device.  
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket handler = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            handler.Shutdown(SocketShutdown.Both);
            //handler.Close();

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public static int Main()
    {
        var directory = System.IO.Directory.GetCurrentDirectory();
        string connString = @"Data Source = (LocalDB)\MSSQLLocalDB; AttachDbFilename=C:\Users\dmitr\source\repos\NetworkChat\Server\Database.mdf; Integrated Security = True; MultipleActiveResultSets=True";
        connection = new SqlConnection(connString);
        try
        {
            connection.Open();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        StartListening();
        connection.Close();
        return 0;
    }
}