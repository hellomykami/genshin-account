using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace GenshinAccount
{
    [Serializable]
    public class YSAccount
    {
        public string Name { get; set; }

        public string MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810 { get; set; }

        public string GENERAL_DATA_h2389025596 { get; set; }

        public static YSAccount ReadFromDisk(string name)
        {
            string p = Path.Combine(Application.StartupPath, "UserData", name);
            string json = File.ReadAllText(p);
            YSAccount acct = new JavaScriptSerializer().Deserialize<YSAccount>(json);
            return acct;
        }

        public void WriteToDisk()
        {
            File.WriteAllText(Path.Combine(Application.StartupPath, "UserData", Name), new JavaScriptSerializer().Serialize(this));
        }

        public static void DeleteFromDisk(string name)
        {
            File.Delete(Path.Combine(Application.StartupPath, "UserData", name));
        }

        public static YSAccount ReadFromRegedit(bool needSettings)
        {
            YSAccount acct = new YSAccount();
            acct.MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810 = GetStringFromRegedit("MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810");
            if(needSettings)
            {
                acct.GENERAL_DATA_h2389025596 = GetStringFromRegedit("GENERAL_DATA_h2389025596");
            }
            return acct;
        }

        public void WriteToRegedit()
        {
            if (string.IsNullOrWhiteSpace(MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810))
            {
                MessageBox.Show("保存账户内容为空", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                SetStringToRegedit("MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810", MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810);
                if (!string.IsNullOrWhiteSpace(GENERAL_DATA_h2389025596))
                {
                    SetStringToRegedit("GENERAL_DATA_h2389025596", GENERAL_DATA_h2389025596);
                }

            }
        }

        private static string GetStringFromRegedit(string key)
        {
            object value = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\miHoYo\Genshin Impact", key, "");
            return Encoding.UTF8.GetString((byte[])value);
        }

        private static void SetStringToRegedit(string key, string value)
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\miHoYo\Genshin Impact", key, Encoding.UTF8.GetBytes(value));
        }

    }
}
