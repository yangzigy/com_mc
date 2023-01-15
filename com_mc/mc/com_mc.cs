using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Reflection;
using System.IO;
using cslib;
using System.Collections;

namespace com_mc
{
	public class DataDes //参数显示时的额外定义
	{
		public string name { get; set; } = ""; //显示名称(唯一)
		public ParaValue val {get;set;} //参数引用
		public bool is_cv { get; set; } = false; //是否显示曲线
		public bool is_dis { get; set; } = true; //是否显示，若是按钮的从属，则可以不显示

		public bool is_val=true; //是否能按值处理(决定是否可以显示曲线)
		public int dis_data_len { get; set; } = 0; //显示数据长度

		public int update_times = 0; //刷新倒计时

		public DataDes(ParaValue v)
		{
			val = v;
			val.update_cb = val_updated; //注册参数的回调函数
			update_cb =void_fun;
			update_dis=void_fun;
		}
		public void val_updated(ParaValue pv) //当此参数改变时
		{
			update_cb(this);
		}
		public override string ToString()
		{
			return val.ToString();
		}
		public delegate void CB(DataDes dd);
		public void void_fun(DataDes dd){}
		public CB update_cb; //数据接收回调
		public CB update_dis; //定时显示回调:定时器直接调用，传递给注册者
	}
	/////////////////////////////////////////////////////////////////////////
	//命令部分
	public enum CmdType //指令类型
	{
		bt, //按键
		text, //文本框
		sw, //开关
		rpl_bool, //带回复的指令
		label, //文本控件
		para, //参数型
	}
	public class CmdDes //指令描述
	{
		public string name { get; set; } //命令显示名称(唯一)
		public string refdname { get; set; } //关联的数据名称
		public string suffixname { get; set; } //后缀参数名称
		public string cmd { get; set; } //命令名称
		public string cmdoff { get; set; } //关闭指令
		public int c_span { get; set; } //列跨度
		public CmdType type { get; set; } //命令名称
		public int repeat_T { get; set; } = 0; //重复周期，若为0，则不是重复指令
		public string dft { get; set; } //默认值
		public CB_s_v get_stat= void_fun; //获取此指令对象状态的回调函数

		public delegate string CB_s_v();
		public static string void_fun() { return ""; }
		public CmdDes()
		{
			name="";
			refdname = "";
			cmd="";
			cmdoff = "";
			c_span = 1;
			suffixname = "";
			type =CmdType.bt;
			dft="";
		}
	}
	//测控总体
	public class Com_MC //通用测控类
	{
		public Dictionary<string, DataDes> dset { get; set; } = new Dictionary<string, DataDes>(); //用于显示参数的数据列表,key为数据项的名称
		public Dictionary<string, CmdDes> cmds { get; set; } = new Dictionary<string, CmdDes>(); //指令列表,key为数据项的名称
		public MC_Prot mc_prot = new MC_Prot(); //测控架构

		public CM_Plugin_Interface pro_obj = null; //无插件时的处理对象
		public void ini(Dictionary<string, object> v) //初始化
		{
			ini_mc(v);//初始化测控体系，并对参数的显示进行额外配置
			/////////////////////////////////////////////////////////////////////
			//_so_tx_cb = new CM_Plugin_Interface.DllcallBack(send_data); //构造不被回收的委托
			try
			{
				FileInfo fi = new FileInfo(Config.config.plugin_path); //已经变成绝对路径了
				Assembly assembly = Assembly.LoadFrom(fi.FullName); //重复加载没事
				string fname = "com_mc." + fi.Name.Replace(fi.Extension, ""); //定义：插件dll中的类名是文件名
				foreach (var t in assembly.GetExportedTypes())
				{
					if (t.FullName == fname)
					{
						pro_obj = Activator.CreateInstance(t) as CM_Plugin_Interface;
					}
				}
				if (pro_obj == null) throw new Exception();
			}
			catch
			{
				pro_obj = new CM_Plugin_Interface();
			}
			pro_obj.ini(MainWindow.mw.send_data, MainWindow.mw.rx_line, MainWindow.mw.rx_pack); //无插件的情况，发送函数、接收函数
			pro_obj.fromJson(Config.config.syn_pro); //帧同步部分初始化
			if (Config.config.encoding == "utf8") pro_obj.cur_encoding = Encoding.UTF8; //根据配置变换编码
			//配置初始化指令
			foreach (var item in Config.config.ctrl_cmds)
			{
				MainWindow.mw.ctrl_cmd(item);
			}
		}
		public void ini_mc(Dictionary<string, object> v) //初始化测控体系，并对参数的显示进行额外配置
		{
			//判断协议配置的方式
			if (v.ContainsKey("filename")) //若是从文件加载的
			{
				try
				{
					string s = v["filename"] as string;
					s = Tool.relPath_2_abs(Config.configPath, s); //都是以配置文件为基础的
					object t = Tool.load_json_from_file<Dictionary<string, object>>(s);
					Tool.dictinary_update(ref t, v); //用软件配置更新协议配置文件里加载的配置
					v = t as Dictionary<string, object>;
				}
				catch (Exception e)
				{
					//MessageBox.Show(e.ToString());
				}
			}
			mc_prot.formJson(v); //初始化测控体系
			foreach (var item in mc_prot.para_dict) //将参数列表复制到显示参数表
			{
				DataDes td = new DataDes(item.Value);
				td.name = item.Key;
				dset[item.Key] = td;
			}
			if (v.ContainsKey("para_dict")) //重新解析参数字典的配置，拿出显示的配置
			{
				ArrayList list = v["para_dict"] as ArrayList;
				foreach (var item in list)
				{
					var tv = item as Dictionary<string, object>;
					string s = tv["name"] as string;
					DataDes td = dset[s];
					if (tv.ContainsKey("is_cv")) td.is_cv = ((int)tv["is_cv"]) != 0;
					if (tv.ContainsKey("is_dis")) td.is_dis = ((int)tv["is_dis"]) != 0;
					if (tv.ContainsKey("dis_data_len")) td.dis_data_len = (int)tv["dis_data_len"];
				}
			}
		}
		public void clear()
		{
			dset.Clear();
			cmds.Clear();
			mc_prot.clear();
		}
	}
}

