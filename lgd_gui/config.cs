using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;

namespace lgd_gui
{
	public class Config
	{
		public Config()
		{
			dis_data_len = 1000;
			uart_b=115200;
			ctrl_cols = 2; //控制按钮默认2列
			svar_ui_h = 0; //传感变量显示区高度，为0则为自动
			cmd_ui_w = 0; //指令区域宽度
			mv_w = 0;
			mv_h = 0;
			bt_margin=2; //按钮间距
			dset = new List<DataDes>();
			cmds = new List<CmdDes>();
			ctrl_cmds = new List<string>();
		}
		public static Config load(string s)
		{
			try
			{
				StreamReader sr = new StreamReader(s);
				string sbuf = sr.ReadToEnd();
				//先把注释去了
				Regex r = new Regex("//.*");
				sbuf=r.Replace(sbuf, "");
				//反串行化
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
		public int uart_b { get; set; } //串口波特率
		public int ctrl_cols { get; set; } //控制按钮的列数
		public int svar_ui_h { get; set; } //传感变量区域高度
		public int cmd_ui_w { get; set; } //指令区域宽度
		public int mv_w { get; set; } //主窗体宽
		public int mv_h { get; set; } //主窗体高
		public int bt_margin { get; set; } //按钮的间距
		public List<DataDes> dset { get; set; } //通用测控对象
		public List<CmdDes> cmds { get; set; } //指令列表
		public List<string> ctrl_cmds { get; set; } //界面控制的指令
	}
}
