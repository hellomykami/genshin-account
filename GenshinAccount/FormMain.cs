using GenshinAccount.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GenshinAccount
{
    public partial class FormMain : Form
    {
        private readonly string userDataPath = Path.Combine(Application.StartupPath, "UserData");
        private string thisVersion;
        private List<ToolStripMenuItem> acctMenuItemList = new List<ToolStripMenuItem>();

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // 标题加上版本号
            string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if (currentVersion.Length > 3)
            {
                thisVersion = currentVersion.Substring(0, 3);
                currentVersion = " v" + thisVersion;
            }
            this.Text += currentVersion;
            GAHelper.Instance.RequestPageView($"/acct/main/{thisVersion}", $"进入{thisVersion}版本原神账户切换工具主界面");


            chkAutoStartYS.Checked = Properties.Settings.Default.AutoRestartYSEnabled;
            chkSkipTips.Checked = Properties.Settings.Default.SkipTipsEnabled;
            txtStartParam.Text = Properties.Settings.Default.YSStartParam;
            txtDllPath.Text = Properties.Settings.Default.InjectDllPath;
            chkInjectDll.Checked = Properties.Settings.Default.InjectDllEnable;
            notifyIcon.Visible = chkMinimizeToNotifyArea.Checked = Properties.Settings.Default.MinimizeToNotifyAreaEnabled;


            if (string.IsNullOrEmpty(Properties.Settings.Default.YSInstallPath))
            {
                txtPath.Text = FindInstallPathFromRegistry();
            }
            else
            {
                txtPath.Text = Properties.Settings.Default.YSInstallPath;
            }

            lvwAcct.Columns[0].Width = lvwAcct.Width;
            ImageList imageList = new ImageList();
            imageList.ImageSize = new Size(10, 20);
            lvwAcct.SmallImageList = imageList;
            RefreshList();

        }

        private void btnSaveCurr_Click(object sender, EventArgs e)
        {
            FormInput form = new FormInput();
            form.ShowDialog();
            RefreshList();
        }

        private void RefreshList()
        {
            contextMenuStrip1.Items.Clear();
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.显示主界面ToolStripMenuItem,
                this.退出ToolStripMenuItem,
                this.toolStripSeparator1});

            if (!Directory.Exists(userDataPath))
            {
                Directory.CreateDirectory(userDataPath);
            }
            lvwAcct.Items.Clear();
            acctMenuItemList.Clear();
            DirectoryInfo root = new DirectoryInfo(userDataPath);
            FileInfo[] files = root.GetFiles();
            foreach (FileInfo file in files)
            {
                lvwAcct.Items.Add(new ListViewItem()
                {
                    Text = file.Name
                });
                ToolStripMenuItem m = new ToolStripMenuItem()
                {
                    Name = file.Name,
                    Text = file.Name,
                };
                m.Click += new EventHandler(this.ToolStripMenuClick);
                contextMenuStrip1.Items.Add(m);
                acctMenuItemList.Add(m);
            }

            if (lvwAcct.Items.Count > 0)
            {
                btnDelete.Enabled = true;
                btnSwitch.Enabled = true;
            }
            else
            {
                btnDelete.Enabled = false;
                btnSwitch.Enabled = false;
            }
        }

        private void ToolStripMenuClick(object sender, EventArgs e)
        {
            Switch((sender as ToolStripMenuItem).Text, chkAutoStartYS.Checked);
            foreach (ToolStripMenuItem menuItem in acctMenuItemList)
            {
                menuItem.Checked = false;
            }
           (sender as ToolStripMenuItem).Checked = true;
        }

        private void btnSwitch_Click(object sender, EventArgs e)
        {
            if (lvwAcct.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要切换的账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = lvwAcct.SelectedItems[0]?.Text;
            Switch(name, chkAutoStartYS.Checked);
        }

        private bool InjectDll(IntPtr hProc)
        {
            IntPtr hKernel = DllUtils.GetModuleHandle("kernel32.dll");
            if (hKernel == IntPtr.Zero)
            {
                MessageBox.Show("kernel32.dll模块地址寻找失败");
                return false;
            }
            IntPtr pLoadLibrary = DllUtils.GetProcAddress(hKernel, "LoadLibraryA");
            if (pLoadLibrary == IntPtr.Zero)
            {
                MessageBox.Show("LoadLibraryA地址获取失败");
                return false;
            }
            IntPtr pDllPath = DllUtils.VirtualAllocEx(hProc, IntPtr.Zero,
                (uint)((txtDllPath.Text.Length + 1) * Marshal.SizeOf(typeof(char))), 0x1000 | 0x2000, 0x4);
            if (pDllPath == IntPtr.Zero)
            {
                MessageBox.Show(String.Format("申请内存地址失败 : {0}", Marshal.GetLastWin32Error()));
                return false;
            }
            bool writeResult = DllUtils.WriteProcessMemory(hProc, pDllPath,
                Encoding.Default.GetBytes(txtDllPath.Text), (uint)((txtDllPath.Text.Length + 1) * Marshal.SizeOf(typeof(char))), out _);
            if (!writeResult)
            {
                MessageBox.Show(String.Format("写进程内存失败 : {0}", DllUtils.GetLastError()));
                return false;
            }
            IntPtr hThread = DllUtils.CreateRemoteThread(hProc, IntPtr.Zero, 0, pLoadLibrary, pDllPath, 0, IntPtr.Zero);
            if (hThread == IntPtr.Zero)
            {
                MessageBox.Show(String.Format("创建远程线程失败 : {0}", Marshal.GetLastWin32Error()));
                DllUtils.VirtualFreeEx(hProc, pDllPath, 0, 0x8000);
                return false;
            }
            if (DllUtils.WaitForSingleObject(hThread, 2000) == IntPtr.Zero)
            {
                DllUtils.VirtualFreeEx(hProc, pDllPath, 0, 0x8000);
            }
            DllUtils.CloseHandle(hThread);
            return true;
        }

        private void StartGameNomal()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = Path.Combine(txtPath.Text, "Genshin Impact Game", "YuanShen.exe");
            startInfo.Verb = "runas";
            startInfo.Arguments = txtStartParam.Text;
            Process.Start(startInfo);
        }

        private void StartGame()
        {
            if (string.IsNullOrEmpty(txtPath.Text))
            {
                MessageBox.Show("游戏路径为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                chkAutoStartYS.CheckState = CheckState.Unchecked;
            }
            else
            {
//                 if (YuanShenIsRunning())
//                 {
//                     MessageBox.Show("原神正在运行，请先关闭原神进程后再试！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
//                     return;
//                 }
                try
                {
                    if (!chkInjectDll.Checked)
                    {
                        StartGameNomal();
                        return;
                    }
                    if (string.IsNullOrEmpty(txtDllPath.Text))
                    {
                        MessageBox.Show("Dll路径为空,正常启动游戏", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        chkInjectDll.CheckState = CheckState.Unchecked;
                        StartGameNomal();
                        return;
                    }
                    bool TokenRet = DllUtils.OpenProcessToken(DllUtils.GetCurrentProcess(), 0xF00FF, out IntPtr hToken);
                    var si = new DllUtils.STARTUPINFOEX();
                    si.StartupInfo.cb = Marshal.SizeOf(si);
                    if (hToken == IntPtr.Zero)
                    {
                        MessageBox.Show("提权失败,正常启动游戏"); 
                        StartGameNomal();
                        return;
                    }
                    var pExporer = Process.GetProcessesByName("explorer")[0];
                    if (pExporer == null)
                    {
                        MessageBox.Show("explorer进程未找到,正常启动游戏");
                        StartGameNomal();
                        return;
                    }
                    IntPtr handle = DllUtils.OpenProcess(0xF0000 | 0x100000 | 0xFFFF, false, (uint)pExporer.Id);
                    var lpSize = IntPtr.Zero;
                    DllUtils.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                    si.lpAttributeList = Marshal.AllocHGlobal(lpSize);
                    DllUtils.InitializeProcThreadAttributeList(si.lpAttributeList, 1, 0, ref lpSize);
                    if (DllUtils.UpdateProcThreadAttribute(si.lpAttributeList, 0, (IntPtr)0x00020004, handle, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                    {
                        MessageBox.Show("更新进程的线程属性失败.");
                    }
                    var pi = new DllUtils.PROCESS_INFORMATION();
                    var result = DllUtils.CreateProcessAsUser(hToken, Path.Combine(txtPath.Text, "Genshin Impact Game", "YuanShen.exe").ToString(),
                        txtStartParam.Text, IntPtr.Zero, IntPtr.Zero, false, 0x00080000 | 0x00000004,
                        IntPtr.Zero, Path.Combine(txtPath.Text, "Genshin Impact Game").ToString(), ref si.StartupInfo, out pi);
                    if (!result)
                    {
                        MessageBox.Show("暂停进程启动失败,正常启动");
                        StartGameNomal();
                        return;
                    }
                    DllUtils.DeleteProcThreadAttributeList(si.lpAttributeList);
                    new System.Threading.Thread(() =>
                    {
                        InjectDll(pi.hProcess);
                        System.Threading.Thread.Sleep(2000);
                        DllUtils.ResumeThread(pi.hThread);
                        DllUtils.CloseHandle(pi.hProcess);
                    }).Start();
                }
                catch
                {

                }
            }
        }

        private void Switch(string name, bool autoRestart)
        {
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请选择要切换的账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!autoRestart)
            {
                if (YuanShenIsRunning())
                {
                    MessageBox.Show("原神正在运行，请先关闭原神进程后再切换账号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (chkSkipTips.Checked || MessageBox.Show($"是否要切换为[{name}]", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (autoRestart)
                {
                    var pros = Process.GetProcessesByName("YuanShen");
                    if (pros.Any())
                    {
                        pros[0].Kill();
                    }
                }
                YSAccount acct = YSAccount.ReadFromDisk(name);
                acct.WriteToRegedit();

                if (autoRestart)
                {
                    StartGame();
                }

                if (!chkSkipTips.Checked)
                {
                    MessageBox.Show($"账户[{name}]切换成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (lvwAcct.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要切换的账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = lvwAcct.SelectedItems[0]?.Text;
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请选择要切换的账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            YSAccount.DeleteFromDisk(name);
            RefreshList();
        }

        private bool YuanShenIsRunning()
        {
            var pros = Process.GetProcessesByName("YuanShen");
            if (pros.Any())
            {
                return true;
            }
            else
            {
                pros = Process.GetProcessesByName("GenshinImpact");
                return pros.Any();
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/babalae/genshin-account");
        }

        private void lvwAcct_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo info = lvwAcct.HitTest(e.X, e.Y);
            if (info.Item != null)
            {
                Switch(info.Item.Text, chkAutoStartYS.Checked);
            }
        }

        /// <summary>
        /// 从注册表中寻找安装路径
        /// </summary>
        /// <param name="uninstallKeyName">
        /// 安装信息的注册表键名 原神
        /// </param>
        /// <returns>安装路径</returns>
        public static string FindInstallPathFromRegistry()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\原神"))
                {
                    if (key == null)
                    {
                        return null;
                    }
                    object installLocation = key.GetValue("InstallPath");
                    if (installLocation != null && !string.IsNullOrEmpty(installLocation.ToString()))
                    {
                        return installLocation.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return null;
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            Properties.Settings.Default.AutoRestartYSEnabled = chkAutoStartYS.Checked;
            Properties.Settings.Default.SkipTipsEnabled = chkSkipTips.Checked;
            Properties.Settings.Default.YSInstallPath = txtPath.Text;
            Properties.Settings.Default.YSStartParam = txtStartParam.Text;
            Properties.Settings.Default.InjectDllPath = txtDllPath.Text;
            Properties.Settings.Default.InjectDllEnable = chkInjectDll.Checked;
            Properties.Settings.Default.MinimizeToNotifyAreaEnabled = chkMinimizeToNotifyArea.Checked;
            Properties.Settings.Default.Save();
        }

        private void btnChoosePath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "请选择原神安装路径";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(dialog.SelectedPath) && File.Exists(Path.Combine(dialog.SelectedPath, "YuanShen.exe")))
                {
                    dialog.SelectedPath = dialog.SelectedPath.Replace("Genshin Impact Game", "");
                }
                if (string.IsNullOrEmpty(dialog.SelectedPath) || !File.Exists(Path.Combine(dialog.SelectedPath, "Genshin Impact Game", "YuanShen.exe")))
                {
                    MessageBox.Show("无法在该文件夹中找到原神启动程序，请选择正确的原神安装路径!");
                }
                else
                {
                    txtPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void FormMain_SizeChanged(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && notifyIcon.Visible)
            {
                this.ShowInTaskbar = false;
            }
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                //notifyIcon.Visible = false;
            }
            this.Activate();
        }

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Dispose();
            this.Close();
        }

        private void 显示主界面ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon_DoubleClick(sender, e);
        }

        private void RefreshContextMenuStrip()
        {
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.显示主界面ToolStripMenuItem,
                this.退出ToolStripMenuItem,
                this.toolStripSeparator1});
        }

        private void chkMinimizeToNotifyArea_CheckedChanged(object sender, EventArgs e)
        {
            notifyIcon.Visible = chkMinimizeToNotifyArea.Checked;
        }

        private void btnDllPath_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "应用程序扩展 (*.dll)|*.dll";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtDllPath.Text = openFileDialog.FileName;
                }
            }
        }

        private void btnStartGame_Click(object sender, EventArgs e)
        {
            StartGame();
        }
    }
}
