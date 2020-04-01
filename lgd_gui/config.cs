using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;

namespace lgd_gui
{
	public class Config
	{
		public Config()
		{
			dis_data_len = 1000;
		}
		public static Config load(string s)
		{
			try
			{
				StreamReader sr = new StreamReader(s);
				string sbuf = sr.ReadToEnd();
				var t = json_ser.Deserialize<Config>(sbuf);
				sr.Close();
				return t;
			}
			catch (Exception e)
			{
				MessageBox.Show(e.ToString());
			}
			return null;
		}
		public void save(string s)
		{
			string jsonString = json_ser.Serialize(this);
			try
			{
				StreamWriter file = new StreamWriter(s);
				file.WriteLine(jsonString);
				file.Close();
			}
			catch (Exception ex)
			{
			}
		}
		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
//配置内容
		public int dis_data_len { get; set; } //显示数据长度
		public List<DataDes> dset { get; set; } //通用测控对象
		public List<CmdDes> cmds { get; set; } //指令列表
	}
}
