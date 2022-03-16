using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using cslib;

namespace com_mc
{
	public class Config
	{
		public static Config config = new Config();     // 存放系统设置
		public static string configPath =AppDomain.CurrentDomain.BaseDirectory;
		public static Config load(string s)
		{
			try
			{
				return Tool.load_json_from_file<Config>(s);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.ToString());
			}
			return null;
		}
		public void save(string s)
		{
			string jsonString = Tool.json_ser.Serialize(this);
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
//配置内容
		//显示控制
		public string title_str { get; set; } = "通用上位机"; //软件标题
		public int dis_data_len { get; set; } = 1000; //显示数据长度
		public int ctrl_cols { get; set; } = 2; //控制按钮默认2列
		public int svar_ui_h { get; set; } = 0; //传感变量显示区高度，为0则为自动
		public int cmd_ui_w { get; set; } = 0; //指令区域宽度
		public int mv_w { get; set; } = 0; //主窗体宽
		public int mv_h { get; set; } = 0;//主窗体高
		public int bt_margin { get; set; } = 2; //按钮间距
		public Dictionary<string, object> syn_pro { get; set; } = new Dictionary<string, object>(); //帧同步处理
																									//数据结构
		public Dictionary<string, object> prot_cfg { get; set; } = new Dictionary<string, object>(); //通用测控对象
		public List<CmdDes> cmds { get; set; } = new List<CmdDes>(); //指令列表
		//菜单
		public int menu_cols { get; set; } = 2;//菜单控制按钮的列数
		public List<string> ctrl_cmds { get; set; } = new List<string>();//界面控制的指令,程序初始化的时候直接执行
		public List<CmdDes> menu_cmd { get; set; } = new List<CmdDes>();//在菜单栏的指令
		public string menu_name { get; set; } = "";//菜单名
		public string encoding { get; set; } = "utf8";//编码名称（dft为默认编码）
		public string plugin_path { get; set; } = "";//插件路径，相对此配置文件
		//数据输入
		public List<Dictionary<string, object>> data_src { get; set; }=new List<Dictionary<string, object>>();
	}
	public class ConfigList //配置列表
	{
		public List<Config_Prop> cfgs { get; set; }=new List<Config_Prop>() { new Config_Prop()}; //默认一个配置文件
		public static ConfigList load(string s)
		{
			try
			{
				return Tool.load_json_from_file<ConfigList>(s);
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
