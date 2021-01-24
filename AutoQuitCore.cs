using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace AutoQuit
{
    public class AutoQuit: BaseSettingsPlugin<AutoQuitSettings>
    {
        
         private readonly int errmsg_time = 10;
        private ServerInventory flaskInventory = null;

        public override bool Initialise()
        {
            Name = "AutoQuit";
            Input.RegisterKey(Settings.forcedAutoQuit);
            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            flaskInventory = GameController.Game.IngameState.ServerData.GetPlayerInventoryBySlot(InventorySlotE.Flask1);
        }

      

        public void Quit()
        {
            var code = CommandHandler.KillTCPConnectionForProcess(GameController.Window.Process.Id);

            if (code == 317)
            {
                LogError("AutoQuit: Run program from admin or set compatibility to Run as administrator");
            }
        }

        public override void Render()
        {
            base.Render();

            // Panic Quit Key.
            if (Input.IsKeyDown(Settings.forcedAutoQuit))
                Quit();

            var LocalPlayer = GameController.Game.IngameState.Data.LocalPlayer;
            var PlayerHealth = LocalPlayer.GetComponent<Life>();
            if (Settings.Enable && LocalPlayer.IsValid)
            {
                if (Math.Round(PlayerHealth.HPPercentage, 3) * 100 < (Settings.percentHPQuit.Value) && PlayerHealth.CurHP != 0)
                {
                    try
                    {
                        Quit();
                    }
                    catch (Exception)
                    {
                        LogError("Error: Something went wrong!", errmsg_time);
                    }
                }
                if (PlayerHealth.MaxES > 0 && (Math.Round(PlayerHealth.ESPercentage, 3) * 100 < (Settings.percentESQuit.Value)))
                {
                    try
                    {
                        Quit();
                    }
                    catch (Exception)
                    {
                        LogError("Error: Something went wrong!", errmsg_time);
                    }
                }
                if (Settings.emptyHPFlasks && gotCharges())
                {
                    try
                    {
                        Quit();
                    }
                    catch (Exception)
                    {
                        LogError("Error: Something went wrong!", errmsg_time);
                    }
                }
            }
        }
        public bool gotCharges()
        {
            int charges = 0;
            var flaskList = getAllFlaskInfo();
            if (flaskList.Any())
            {
                foreach (Entity flask in flaskList)
                {
                    var CPU = flask.GetComponent<Charges>().ChargesPerUse;
                    var curCharges = flask.GetComponent<Charges>().NumCharges;
                    if (curCharges >= CPU)
                    {
                        charges += curCharges / CPU;
                    }
                }
                if (charges <= 0)
                {
                    return true;
                }
                return false;
            }
            return false;
        }

        public List<Entity> getAllFlaskInfo()
        {
            List<Entity> flaskList = new List<Entity>();

            if (flaskInventory == null)
            {
                flaskInventory = GameController.Game.IngameState.ServerData.GetPlayerInventoryBySlot(InventorySlotE.Flask1);
            }

            for (int i = 0; i < 5; i++)
            {
                var flask = flaskInventory[i, 0]?.Item;
                if (flask != null)
                {
                    var baseItem = GameController.Files.BaseItemTypes.Translate(flask.Path);
                    if (baseItem != null && baseItem.BaseName.Contains("Life Flask"))
                    {
                        flaskList.Add(flask);
                    }
                }
            }
            return flaskList;
        }
    }

    // Taken from ->
    // https://www.reddit.com/r/pathofexiledev/comments/787yq7/c_logout_app_same_method_as_lutbot/
    public static partial class CommandHandler
    {
        public static int KillTCPConnectionForProcess(int ProcessId)
        {
            MibTcprowOwnerPid[] table;
            var afInet = 2;
            var buffSize = 0;
            var ret = GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, afInet, TcpTableClass.TcpTableOwnerPidAll);
            var buffTable = Marshal.AllocHGlobal(buffSize);
            try
            {
                ret = GetExtendedTcpTable(buffTable, ref buffSize, true, afInet, TcpTableClass.TcpTableOwnerPidAll);
                if (ret != 0)
                    return 0;
                var tab = (MibTcptableOwnerPid)Marshal.PtrToStructure(buffTable, typeof(MibTcptableOwnerPid));
                var rowPtr = (IntPtr)((long)buffTable + Marshal.SizeOf(tab.dwNumEntries));
                table = new MibTcprowOwnerPid[tab.dwNumEntries];
                for (var i = 0; i < tab.dwNumEntries; i++)
                {
                    var tcpRow = (MibTcprowOwnerPid)Marshal.PtrToStructure(rowPtr, typeof(MibTcprowOwnerPid));
                    table[i] = tcpRow;
                    rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(tcpRow));

                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffTable);
            }

            //Kill Path Connection
            var PathConnection = table.FirstOrDefault(t => t.owningPid == ProcessId);
            PathConnection.state = 12;
            var ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(PathConnection));
            Marshal.StructureToPtr(PathConnection, ptr, false);
            var tcpEntry = SetTcpEntry(ptr);
            return tcpEntry;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, TcpTableClass tblClass, uint reserved = 0);

        [DllImport("iphlpapi.dll")]
        private static extern int SetTcpEntry(IntPtr pTcprow);

        [StructLayout(LayoutKind.Sequential)]
        public struct MibTcprowOwnerPid
        {
            public uint state;
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] localPort;
            public uint remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] remotePort;
            public uint owningPid;

        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MibTcptableOwnerPid
        {
            public uint dwNumEntries;
            private readonly MibTcprowOwnerPid table;
        }

        private enum TcpTableClass
        {
            TcpTableBasicListener,
            TcpTableBasicConnections,
            TcpTableBasicAll,
            TcpTableOwnerPidListener,
            TcpTableOwnerPidConnections,
            TcpTableOwnerPidAll,
            TcpTableOwnerModuleListener,
            TcpTableOwnerModuleConnections,
            TcpTableOwnerModuleAll
        }
    }
    
   public class AutoQuitSettings : ISettings
    {
        public AutoQuitSettings()
        {
            percentHPQuit = new RangeNode<float>(35f, 0f, 100f);
            percentESQuit = new RangeNode<float>(35f, 0, 100);
            forcedAutoQuit = new HotkeyNode(Keys.F4);
        }

        #region Auto Quit Menu
        [Menu("Select key for Forced Quit", 1)]
        public HotkeyNode forcedAutoQuit { get; set; }
        [Menu("Min % Life to Auto Quit", 2)]
        public RangeNode<float> percentHPQuit { get; set; }
        [Menu("Min % ES Auto Quit", 3)]
        public RangeNode<float> percentESQuit { get; set; }
        [Menu("Quit if HP flasks are empty", 4)]
        public ToggleNode emptyHPFlasks { get; set; } = new ToggleNode(false);
        #endregion

        public ToggleNode Enable { get; set; } = new ToggleNode(false);
    }
}