using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;

namespace com_mc
{
	public class Config
	{
		public Config()
		{
			title_str = "通用上位机";
			dis_data_len = 1000;
			ctrl_cols = 2; //控制按钮默认2列
			svar_ui_h = 0; //传感变量显示区高度，为0则为自动
			cmd_ui_w = 0; //指令区域宽度
			mv_w = 0;
			mv_h = 0;
			bt_margin=2; //按钮间距
			dset = new List<DataDes>();
			cmds = new List<CmdDes>();
			menu_cols = 2;
			ctrl_cmds = new List<string>();
			menu_cmd = new List<CmdDes>();
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
		//显示控制
		public string title_str { get; set; } //软件标题
		public int dis_data_len { get; set; } //显示数据长度
		public int ctrl_cols { get; set; } //控制按钮的列数
		public int svar_ui_h { get; set; } //传感变量区域高度
		public int cmd_ui_w { get; set; } //指令区域宽度
		public int mv_w { get; set; } //主窗体宽
		public int mv_h { get; set; } //主窗体高
		public int bt_margin { get; set; } //按钮的间距
		//数据结构
		public List<DataDes> dset { get; set; } //通用测控对象
		public List<CmdDes> cmds { get; set; } //指令列表
		//菜单
		public int menu_cols { get; set; } //菜单控制按钮的列数
		public List<string> ctrl_cmds { get; set; } //界面控制的指令,程序初始化的时候直接执行
		public List<CmdDes> menu_cmd { get; set; } //在菜单栏的指令
		public string menu_name { get; set; } //菜单名
		public string encoding { get; set; } //编码名称（dft为默认编码）
		public string plugin_path { get; set; } //插件路径，相对此配置文件
		//数据输入
		public List<Dictionary<string, object>> data_src { get; set; }
	}
	public class ConfigList //配置列表
	{
		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();

		public List<Config_Prop> cfgs { get; set; }=new List<Config_Prop>() { new Config_Prop()}; //默认一个配置文件
		public ConfigList()
		{

		}
		public static ConfigList load(string s)
		{
			try
			{
				StreamReader sr = new StreamReader(s);
				string sbuf = sr.ReadToEnd();
				//先把注释去了
				Regex r = new Regex("//.*");
				sbuf = r.Replace(sbuf, "");
				//反串行化
				var t = json_ser.Deserialize<ConfigList>(sbuf);
				sr.Close();
				return t;
			}
			catch (Exception e)
			{
				//MessageBox.Show(e.ToString());
			}
			return new ConfigList(); //没有文件，就加载默认配置
		}

	}
	public class Config_Prop //配置的属性
	{
		public string fname { get; set; } = "config.txt"; //配置文件名（相对路径）
		public string des { get; set; } //配置说明
	}
}