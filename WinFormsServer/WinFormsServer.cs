﻿using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.ObjectModel;

namespace SignalRChat
{
    /// <summary>
    /// WinForms host for a SignalR server. The host can stop and start the SignalR
    /// server, report errors when trying to start the server on a URI where a
    /// server is already being hosted, and monitor when clients connect and disconnect. 
    /// The hub used in this server is a simple echo service, and has the same 
    /// functionality as the other hubs in the SignalR Getting Started tutorials.
    /// </summary>
    public partial class WinFormsServer : Form
    {
        private IDisposable SignalR { get; set; }
        const string ServerURI = "http://helpdesk.hunet.co.kr:8080";

        internal WinFormsServer()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Calls the StartServer method with Task.Run to not
        /// block the UI thread. 
        /// </summary>
        private void ButtonStart_Click(object sender, EventArgs e)
        {
            WriteToConsole("Starting server...");
            ButtonStart.Enabled = false;
            Task.Run(() => StartServer());
        }

        /// <summary>
        /// Stops the server and closes the form. Restart functionality omitted
        /// for clarity.
        /// </summary>
        private void ButtonStop_Click(object sender, EventArgs e)
        {
            //SignalR will be disposed in the FormClosing event
            Close();
        }

        /// <summary>
        /// Starts the server and checks for error thrown when another server is already 
        /// running. This method is called asynchronously from Button_Start.
        /// </summary>
        private void StartServer()
        {
            try
            {
                SignalR = WebApp.Start(ServerURI);
            }
            catch (TargetInvocationException)
            {
                WriteToConsole("Server failed to start. A server is already running on " + ServerURI);
                //Re-enable button to let user try to start server again
                this.Invoke((Action)(() => ButtonStart.Enabled = true));
                return;
            }
            this.Invoke((Action)(() => ButtonStop.Enabled = true));
            WriteToConsole("Server started at " + ServerURI);
        }
        /// <summary>
        /// This method adds a line to the RichTextBoxConsole control, using Invoke if used
        /// from a SignalR hub thread rather than the UI thread.
        /// </summary>
        /// <param name="message"></param>
        internal void WriteToConsole(String message)
        {
            if (RichTextBoxConsole.InvokeRequired)
            {
                this.Invoke((Action)(() =>
                    WriteToConsole(message)
                ));
                return;
            }
            RichTextBoxConsole.AppendText(message + Environment.NewLine);
        }

        private void WinFormsServer_FormClosing(object sender, FormClosingEventArgs e)
        {

            if (SignalR != null)
            {
                SignalR.Dispose();
            }
        }
    }
    /// <summary>
    /// Used by OWIN's startup process. 
    /// </summary>
    class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Make long polling connections wait a maximum of 110 seconds for a
            // response. When that time expires, trigger a timeout command and
            // make the client reconnect.
            GlobalHost.Configuration.ConnectionTimeout = TimeSpan.FromSeconds(110);

            // Wait a maximum of 30 seconds after a transport connection is lost
            // before raising the Disconnected event to terminate the SignalR connection.
            GlobalHost.Configuration.DisconnectTimeout = TimeSpan.FromSeconds(30);

            // For transports other than long polling, send a keepalive packet every
            // 10 seconds. 
            // This value must be no more than 1/3 of the DisconnectTimeout value.
            GlobalHost.Configuration.KeepAlive = TimeSpan.FromSeconds(10);
            //GlobalHost.DependencyResolver.UseRedis("helpdesk.hunet.co.kr", 6379, string.Empty, "myHub");
            app.UseCors(CorsOptions.AllowAll);
            var hubConfiguration = new HubConfiguration
            {

                // You can enable JSONP by uncommenting line below.
                // JSONP requests are insecure but some older browsers (and some
                // versions of IE) require JSONP to work cross domain
                EnableJSONP = true

            };
            //app.MapHubs();
            app.MapSignalR(hubConfiguration);
        }
    }
    public class Group
    {
        public string Name { get; set; }
        public int MaxMember { get { return 2; } }
        public bool IsAvailable { get; set; }
        public List<Client> Clients { get; set; }
        public string AgentId { get; set; }
        public string AgentName { get; set; }
        public string CategoryCd { get; set; }
    }

    public class Agent
    {
        public string CategoryCd { get; set; }
        public string AgentId { get; set; }
        public string ConnectionId { get; set; }
    }

    public class Client
    {
        public string ConnectionId { get; set; }
        public string ClientName { get; set; }
        public string Guid { get; set; }
        public string CategoryCd { get; set; }
    }
    
    /// <summary>
    /// Echoes messages sent using the Send message by calling the
    /// addMessage method on the client. Also reports to the console
    /// when clients connect and disconnect.
    /// </summary>
    public class MyHub : Hub
    {

        public static List<Client> waittingClient = new List<Client>();
        
        private static List<Agent> agentList = new List<Agent>();
        public static List<Group> groupList = new List<Group>();
        public void Send(string name, string message, string groupName)
        {
            if (String.IsNullOrEmpty(groupName))
            {
                var group = groupList.FirstOrDefault(d => d.Clients.Count(o => o.ConnectionId == Context.ConnectionId) > 0);
                groupName = group.Name;
            }

            Clients.Group(groupName).addMessage(name, message);
        }
        public override Task OnConnected()
        {
            Program.MainForm.WriteToConsole("Client connected: " + Context.ConnectionId);
            return base.OnConnected();
        }

        public override Task OnReconnected()
        {
            Program.MainForm.WriteToConsole("Client Reconnected: " + Context.ConnectionId);
            return base.OnReconnected();
        }
        public override Task OnDisconnected(bool isStopCall)
        {

            //대기리스트에서 삭제
            var clien = waittingClient.FirstOrDefault(d => d.ConnectionId == Context.ConnectionId)??new Client();
            waittingClient.Remove(clien);
            if (!String.IsNullOrEmpty(clien.ConnectionId))
            {
                GetWaittingClients(clien.CategoryCd);                
            }

            //상담중리스트에서 삭제
            var client = groupList.FirstOrDefault(d => d.Clients.Count(o => o.ConnectionId == Context.ConnectionId) > 0);
            if (client != null)
            {               
                Groups.Remove(Context.ConnectionId, client.Name);
            }

            var agent = groupList.FirstOrDefault(d => d.AgentId == Context.ConnectionId);
            var agen = agentList.FirstOrDefault(d => d.ConnectionId == Context.ConnectionId)??new Agent();
            agentList.Remove(agen);

            

            if (agent != null)
            {
                groupList.Remove(agent);
                Groups.Remove(Context.ConnectionId, agent.Name);
            }

            Program.MainForm.WriteToConsole("Client disconnected: " + Context.ConnectionId);
            return base.OnDisconnected(isStopCall);
        }
        public void JoinGroup(string name, string groupName)
        {

            Clients.Group(groupName).joinMessage(name, String.Format("Join : {0}", name));
        }

        public void GetGroupList()
        {
            Clients.Caller.getGroupList(groupList.Select(d => d.Name).ToList());
        }
        public enum TransferMessage { 
            Available
            ,NotAvaliable
    }
        public void SessionTranserMessage(string message)
        {
            Clients.Caller.sessionTranserMessage(message);
        }

        public void SessionTransfer(string source, string target)
        {
            var sourceGroup = groupList.FirstOrDefault(d => d.Name == source);
            
            if (sourceGroup != null)
            {
                var targetGroup = groupList.FirstOrDefault(d => d.Name == target && d.IsAvailable);
                if (targetGroup == null)
                {
                    SessionTranserMessage("현재 해당 상담원은 상담중입니다.");
                    return;
                }
                else 
                {
                    List<string> clientIdes = new List<string>();

                    foreach (var client in sourceGroup.Clients)
                    {
                        clientIdes.Add(client.ConnectionId);
                        var clientId = String.Empty;
                        clientId = client.ConnectionId;
                        Groups.Remove(clientId, source);
                        Groups.Add(clientId, target);
                        JoinGroup(client.ClientName, target);
                       
                        targetGroup.IsAvailable = false;
                        targetGroup.Clients.Add(client);                        
                                          
                    }
                    sourceGroup.IsAvailable = true;
                    sourceGroup.Clients = new List<Client>();
                    if (clientIdes.Count() > 0)
                    {
                        Clients.Clients(clientIdes).changeGroupName(target);
                    }
                }     
            }
           
        }
        public void FindGroup(string name, string guid, string categoryCd)
        {
            var reGroup = groupList.FirstOrDefault(d => d.Clients.Count(c => c.Guid == guid) > 0);
            if (reGroup != null)
	        {
                Clients.Caller.findedGroup(reGroup.Name);
                Groups.Add(Context.ConnectionId, reGroup.Name);
                reGroup.Clients.Add(new Client {
                    ConnectionId = Context.ConnectionId,
                    ClientName = name,
                    Guid = guid
                });
                return;
            }
            var group = groupList.Where(d => d.IsAvailable && d.CategoryCd == categoryCd ).FirstOrDefault();
            if (group != null)
            {
                group.IsAvailable = false;
                group.Clients.Add(new Client
                {
                    ConnectionId = Context.ConnectionId,
                    ClientName = name,
                    Guid = guid
                });
                Groups.Add(Context.ConnectionId, group.Name);
            }
            else {
                waittingClient.Add(new Client()
                {
                    ConnectionId = Context.ConnectionId,
                    CategoryCd = categoryCd,
                    Guid = guid,
                    ClientName = name
                });
                GetWaittingClients(categoryCd);
            }
            Clients.Caller.findedGroup(group == null ? "" : group.Name);
        }

        public Task CreateGroup(string name, string groupName, string categoryCd)
        {
            var group = groupList.Where(d => d.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (group == null)
            {
                groupList.Add(new Group()
                {
                    IsAvailable = true,
                    Name = groupName,
                    AgentName = name,
                    Clients = new List<Client>(),
                    CategoryCd ="",
                    AgentId = Context.ConnectionId
                });

            }
            return Groups.Add(Context.ConnectionId, groupName);

        }

        public void LeaveGroup(string name, string groupName)
        {
            Clients.Group(groupName).leaveMessage(name);
        }

        public void DisconnectClient(string groupName)
        {
            var group = groupList.FirstOrDefault(d=>d.Name == groupName);           
            Clients.Group(group.Name).addMessage(group.Clients.Select(c=>c.ClientName).FirstOrDefault(), "접속을 끊었습니다.");
            foreach (var item in group.Clients)
            {
                Groups.Remove(item.ConnectionId, group.Name);
            }
            Groups.Remove(group.AgentId, group.Name);
            groupList.Remove(group);
        }

        public void JoinAgentList(string categoryCd, string id)
        {
            Agent agent = new Agent() {  AgentId = id, CategoryCd =categoryCd, ConnectionId = Context.ConnectionId};
            agentList.Add(agent);
            Clients.Caller.joinAgentList(waittingClient.Where(d => d.CategoryCd == categoryCd));
        }

        public void GetWaittingClients(string categoryCd)
        {            
            Clients.Clients(agentList.Where(d => d.CategoryCd == categoryCd).Select(d => d.ConnectionId).ToList()).getWaittingClients(waittingClient.Where(d => d.CategoryCd == categoryCd).ToList());
        }

    }
}
