using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using cslib;

namespace com_mc
{
	using JD = Dictionary<string, object>;
	public enum ProtType //协议域类型，str之前必须兼容DataType
	{
		u8, u16, u32, u64, //无符号整数
		s8, s16, s32, s64, //有符号整数
		f, df, //float、double
		undef, str, //未定义、字符串
		//插件扩展的时候，可以用undef
		obj, //顺序协议域容器
		loop, //循环协议域
		sw, //选择分支协议域容器
		text, //文本行协议族选择
		tline, //文本行协议域容器
		//校验类型
		check
	}
	//协议相当于结构体定义中的一个变量，协议名称是变量名称。
	//由于每个变量都需要引用参数，所以不需要定义变量的类型。嵌套结构体通过全部展开定义基础数据结构的方式来定义，协议对象无法复用
	public abstract class ProtDom //协议域纯虚父类
	{
		public MC_Prot p_mcp=null; //协议系统的引用
		public PD_Obj father_Dom = null; //上级协议的引用
		public string name { get; set; } = ""; //协议域名称(变量名)(唯一，或没有)
		public int id { get; set; } = 0; //参数的唯一id，在C程序中使用
		public ProtType type { get; set; } = 0; //数据类型,默认是u8（此处无效，在factory处设置为str）
		public string ref_name { get; set; } = "";//引用参数的名称
		public ParaValue ref_para=null; //引用的参数，没有引用会给内部对象，一定会有
		public int len = 0; //缓存本域的数据长度，对于二进制定长协议必须赋值
		public ProtDom(JD v, ProtType t, PD_Obj pd) //从json构造对象
		{ //这里遇到错误就throw出去，不想throw的才判断
			if(pd!=null) p_mcp = pd.p_mcp;
			father_Dom = pd;
			if (v.ContainsKey("id")) id = (int)v["id"];
			if (v.ContainsKey("name")) name = (string)v["name"];
			type = t;
			if(v.ContainsKey("ref_name"))
			{
				ref_name = (string)v["ref_name"];
				//引用参数不对不能崩，只能添加到错误列表
				if (!p_mcp.para_dict.ContainsKey(ref_name)) { }// throw new Exception("无此参数："+ref_name); //引用的参数不对
				else ref_para = p_mcp.para_dict[ref_name]; //参数表先于协议加载
				return;
			}
			//没有引用参数，说明是协议内部结构数据
			var tv = new JD();
			tv["type"] = "u64";
			tv["name"] = "";
			tv["len"] = 8;
			ref_para = new ParaValue_Val(tv, DataType.u64); //创建一个内部参数对象
		}
		//处理数据，输入对象首地址，off：数据起始位置，n:off之后还有多长。
		public abstract int pro(byte[] b, ref int off, int n); //返回0成功，1未完成，2失败
		public static Int64 double_2_s64(double f) //浮点转整型四舍五入
		{
			if (f < 0) f -= 0.5;
			else f += 0.5;
			return (Int64)f;
		}
	}
	public partial class PD_Obj : ProtDom //协议对象
	{
		public List<ProtDom> prot_list = new List<ProtDom>(); //顺序的子协议域
		public Dictionary<string, ProtDom> prot_dict = new Dictionary<string, ProtDom>(); //协议帧实体字典,由名称索引
		public int pro_ind = 0; //处理到哪个协议域了，下次进来从这开始
		public PD_Obj(JD v, ProtType t, PD_Obj pd) : base(v, t, pd)
		{
			if (!v.ContainsKey("prot_list")) return;
			prot_list.Clear();
			ArrayList list = v["prot_list"] as ArrayList;
			foreach (var item in list)
			{
				string s = item as string; //直接给了这个协议域的名称，需要是之前定义过的，在结构列表中找。
				var tv = item as JD;
				if (tv == null) //若是定义的复用的结构，
				{ //此时应该把结构名变成实体名，否则多个同结构实体会重名。把JD复制一遍，去掉name
					if (!p_mcp.struct_dict.ContainsKey(s)) throw new Exception("无顶层协议："+s);
					tv = p_mcp.struct_dict[s] as JD;
					tv = Tool.jd_clone(tv) as JD;
					tv.Remove("name");
				}
				//else //若是把子协议域直接写出来，则tv已经有东西了
				if (tv.ContainsKey("name")) s = tv["name"] as string; //若有指定名称
				else s = string.Format("_{0}", prot_list.Count); //_2的形式，生成默认成员名称
				var p=gennerate_sub(s, tv); //递归生成子节点，s为名称
				var tp = p as PD_Node;
				if (tp != null) //若是叶子节点
				{
					len += tp.skip_n;
				}
				len += p.len;
			}
			//更新输入结构
			v["len"] = len;
		}
		public ProtDom gennerate_sub(string subname,JD tv) //生成子节点
		{
			tv["name"] = subname; //无论这个结构是不是复用的，实例化的时候都是本节点的子
			var p = MC_Prot.factory(tv, this); //递归创建自己的子协议域
			p.father_Dom = this; //给上层引用赋值
			prot_dict[subname] = p; //注册到本级名称字典
			prot_list.Add(p); //无论是否处理需要，遍历子节点的时候都需要
			return p;
		}
		public ProtDom get_pd(string path) //根据相对路径获取协议域引用
		{
			int i = 0;
			int ind = path.IndexOf('/');
			if (ind < 0)
			{
				if (prot_dict.ContainsKey(path)) //若直接就是本级路径
				{
					return prot_dict[path];
				}
				else if (path.StartsWith("_") && int.TryParse(path.Substring(1), out i)) //如果是_2的形式，可以直接提取数字
				{
					return prot_list[i];
				}
				throw new Exception(string.Format("路径错误:{0}:{1}",name,path));
			}
			string ts = path.Substring(0, ind); //只处理第一个
			if (ts == ".." && father_Dom != null) return father_Dom.get_pd(path.Substring(ind+1)); //上级
			else if(prot_dict.ContainsKey(ts)) //若是子节点
			{
				return (prot_dict[ts] as PD_Obj).get_pd(path.Substring(ind+1));
			}
			else if(ts.StartsWith("_")) //如果是_2的形式，可以直接提取数字
			{
				if(int.TryParse(ts.Substring(1), out i))
				{
					return (prot_list[i] as PD_Obj).get_pd(path.Substring(ind + 1));
				}
			}
			throw new Exception(string.Format("路径错误:{0}:{1}", name, path));
		}
	}
	public partial class PD_Switch : PD_Obj //选择协议域方式
	{
		public string ref_type = ""; //引用的协议域，用于确定包类型
		public string cfg_str_type = ""; //配置中，协议索引的字符串类型。空为10进制，hex为10进制
		public int skip_n = 0; //对子协议，额外向后跳的字节数（若是对多种协议的选择，可以从0开始，从新解析）

		public PD_Node ref_dom; //为了方便直接建立确定包类型的协议域
		public Dictionary<int, ProtDom> prot_map=new Dictionary<int, ProtDom>(); //各协议描述符头部，由int对协议进行索引
		public PD_Switch(JD v, ProtType t, PD_Obj pd) : base(v, t,pd)
		{ //switch没有obj的prot_list
			ref_type = v["ref_type"] as string;
			if(v.ContainsKey("cfg_str_type")) cfg_str_type = v["cfg_str_type"] as string; //
			if (v.ContainsKey("skip_n")) skip_n = (int)v["skip_n"];
			var dict = v["prot_map"] as JD; //各协议由字典组织
			foreach (var item in dict) //这里记录的是： 整数字符：结构名
			{
				int k = 0;
				if(cfg_str_type=="hex")
				{
					k= int.Parse(item.Key, System.Globalization.NumberStyles.HexNumber);
				}
				else int.TryParse(item.Key, out k);
				//创建子节点
				string s = item.Value as string; //结构名
				var tv = p_mcp.struct_dict[s] as JD; //switch用的都是复用的结构的名称
				var p = gennerate_sub(s, tv);
				prot_map[k] = p;
				//引用参考域，要求事先定义
				ref_dom = get_pd(ref_type) as PD_Node;
			}
		}
	}
	public partial class PD_Loop : PD_Obj //重复协议域方式，实体只有一份，重复调用，重复次数可配置可引用
	{
		public string ref_len = ""; //引用的协议域，用于确定重复次数

		public PD_Node ref_dom; //引用的协议域，用于确定重复次数，为了方便
		public int loop_ind = 0; //循环次数
		public PD_Loop(JD v, ProtType t, PD_Obj pd) : base(v, t,pd)
		{
			if (v.ContainsKey("ref_len"))
			{
				ref_len = v["ref_len"] as string;
				ref_dom = get_pd(ref_len) as PD_Node;
			}
			else //需要直接指定
			{
				var para = ref_para as ParaValue_Val;
				para.data.ds32 = (int)v["loop_n"];
			}
		}
	}
	public partial class MC_Prot //测控参数体系的实现
	{
		public JD struct_dict = new JD(); //用结构名称查询结构字典，用于结构复用

		public Dictionary<string, ParaValue> para_dict = new Dictionary<string, ParaValue>(); //自己用的参数字典
		public Dictionary<string, ParaValue> para_dict_out = new Dictionary<string, ParaValue>(); //输出的参数字典

		public List<Sync_Prot> prot_root_list = new List<Sync_Prot>(); //协议族根节点列表，树形组织所有结构对象。直接用帧同步对象来做引用（在cmlog日志中id从1开始）
		public PD_LineSwitch text_root = null; //文本协议节点(默认在cmlog中的id=0)

		public PD_Obj void_obj; //空的obj用于初始化时赋值

		public void fromJson(JD v) //初始化
		{
			clear();
			//先构造一个空的obj
			JD void_v = new JD();
			void_v["type"] = "obj";
			void_obj = factory(void_v, null) as PD_Obj;
			void_obj.p_mcp = this; //主要就为了给这个赋值
			try
			{
			//1、最先初始化参数字典，后边协议初始化要引用
				ArrayList list = v["para_dict"] as ArrayList;
				foreach (var item in list)
				{
					var tv = item as JD;
					string s = tv["name"] as string;
					para_dict[s] = ParaValue.factory(tv); //构建参数
					para_dict_out[s] = ParaValue.factory(tv); //构建输出参数
				}
			//2、构造结构定义，只是缓存(文本和二进制协议都有)
				if (v.ContainsKey("struct_dict"))
				{
					struct_dict = v["struct_dict"] as JD; //记录结构定义
					foreach (var item in struct_dict) //把name给到字典里
					{
						var tv = item.Value as JD;
						if (!tv.ContainsKey("name")) tv["name"] = item.Key;
						prot_json_set_type(tv); //在json配置中为协议域设置省略的type
					}
				}
			//3、二进制根节点初始化，递归建立所有实体。若没有说明没有二进制协议
				if (v.ContainsKey("prot_roots"))
				{
					ArrayList li = v["prot_roots"] as ArrayList; //读取各协议族的根节点
					foreach (var item in li) //对于每一个根
					{
						string s = item as string;
						PD_Obj obj = factory(struct_dict[s] as JD, void_obj) as PD_Obj; //根节点递归建立
						if (obj == null) throw new Exception("根节点不是obj");
						prot_root_list.Add(new Sync_Prot(obj)); //建立帧同步对象
						prot_root_list[prot_root_list.Count - 1].ref_prot_root_id = prot_root_list.Count; //给参考通道号赋值
					}
				}
			//4、初始化文本协议
				if(v.ContainsKey("tl_root"))
				{
					var tv = v["tl_root"] as JD;
					tv["type"] = "text";
					text_root = factory(tv, void_obj) as PD_LineSwitch;
					//由于文本协议不是增量输入，所以构建完成后，需要把协议域所引用的参数改成para_dict_out
					foreach (var item in text_root.prot_dict) //对于每一个行协议
					{
						var tmp = item.Value as PD_LineObj;
						foreach (var it in tmp.prot_list) //对于每一个列
						{
							if(para_dict_out.ContainsKey(it.ref_para.name)) //在输出的参数字典中如果存在这个名字
							{
								it.ref_para = para_dict_out[it.ref_para.name];
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				MessageBox.Show("message: " + e.Message);
			}
		}
		public JD toJson() //使用json保存当前配置
		{ //构造一个字典，包含：para_dict、struct_dict、prot_roots、tl_root 4部分
			List<string> rootlist = new List<string>();
			foreach (var item in prot_root_list)
			{
				rootlist.Add(item.rootpd.name);
			}
			List<string> textlist = new List<string>();
			if(text_root != null)
			{
				for (int i=0;i<text_root.prot_list.Count;i++)
				{
					textlist.Add(text_root.prot_list[i].name);
				}
			}
			return toJson(para_dict, struct_dict, rootlist, textlist);
		}
		static public JD toJson(
			Dictionary<string, ParaValue> para_d, //1、参数字典
			JD struct_d, //2、结构定义
			List<string> rootlist, //3、二进制根节点名称列表
			List<string> textlinelist) //4、文本行协议列表
		{
			JD v = new JD();
		//1、参数字典
			var t = new ArrayList();
			foreach (var item in para_d)
			{
				t.Add(item.Value.toJson());
			}
			v["para_dict"] = t;
		//2、结构定义
			v["struct_dict"] = struct_d;
		//3、二进制根节点
			var al = new ArrayList();
			foreach (var item in rootlist) al.Add(item);
			if (al.Count > 0) v["prot_roots"] = al;
		//4、文本根节点
			if (textlinelist.Count>0)
			{
				var tv = new JD();
				al.Clear();
				for (int i = 0; i < textlinelist.Count; i++)
				{
					al.Add(textlinelist[i]);
				}
				tv["prot_list"] = al;
				v["tl_root"] = tv;
			}
			return v;
		}
		public void clear()
		{
			struct_dict.Clear();
			text_root = null;
			para_dict.Clear();
			para_dict_out.Clear();
			prot_root_list.Clear();
		}
		static public void prot_json_set_type(JD v) //在json配置中为协议域设置省略的type
		{
			if (v.ContainsKey("type")) { }
			else if (v.ContainsKey("prot_list")) //若没指定type，但有子节点
			{
				if (v.ContainsKey("col_n")) v["type"] = "tline"; //若是文本行协议
				else v["type"] = "obj"; //若是obj
			}
			else if (v.ContainsKey("prot_map")) v["type"] = "sw"; //switch也能从属性确定
			else v["type"] = "str"; //既没有显式指定，也不是obj，按默认的来
		}
		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		public static ProtDom factory(JD v, PD_Obj pd) //构建工厂，输入配置，测控协议对象
		{
			ProtType t = ProtType.str; //默认类型,str的不需要写type这个域
			prot_json_set_type(v);
			string s = json_ser.Serialize(v["type"]); //这样取得的字符串带"
			t = json_ser.Deserialize<ProtType>(s); //取得参数类型，enum类型的反串行化需要字符串带"
			switch (t) //若是基础类型
			{
				case ProtType.undef:
				case ProtType.str: return new PD_Str(v, t, pd);
				case ProtType.u8:
				case ProtType.u16:
				case ProtType.u32:
				case ProtType.u64:
				case ProtType.s8:
				case ProtType.s16:
				case ProtType.s32:
				case ProtType.s64:
				case ProtType.f:
				case ProtType.df: return new PD_Node(v, t, pd);
				case ProtType.obj: return new PD_Obj(v, t, pd);
				case ProtType.sw: return new PD_Switch(v, t, pd);
				case ProtType.loop: return new PD_Loop(v, t, pd);
				case ProtType.text: return new PD_LineSwitch(v, t, pd);
				case ProtType.tline: return new PD_LineObj(v, t, pd);
				case ProtType.check: return new PD_Check(v, t, pd);
				default: throw new Exception("type err");
			}
		}
	}
}

