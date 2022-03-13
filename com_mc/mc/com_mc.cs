using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;

namespace com_mc
{
	public class DataDes //参数显示时的额外定义
	{
		public string name { get; set; } = ""; //显示名称(唯一)
		public ParaValue val {get;set;} //参数引用
		public bool is_cv { get; set; } = false; //是否显示曲线
		public bool is_dis { get; set; } = true; //是否显示，若是按钮的从属，则可以不显示

		public bool is_val=true; //是否能按值处理

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
}

