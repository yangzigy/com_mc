using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Windows.Markup.Localizer;
using cslib;

namespace com_mc
{
	using JD = Dictionary<string, object>;
	public class PD_LineObj : PD_Obj //文本行协议对象，不接受二进制输入
	{
		public string head = ""; //协议头，带着$
		public int col_n = 0; //列数
		public string[] split_char_list = new string[0]; //本协议的特殊分隔符（先按默认分隔符确定协议，然后才按自定义的）

		public PD_Str[] protobj_list = null; //一系列顺序的协议域（为了方便调用）
		public PD_LineObj(Dictionary<string, object> v, ProtType t, PD_Obj pd) : base(v, t, pd)
		{
			if (v.ContainsKey("head")) head = v["head"] as string; //读取此行的首部名称
			if (v.ContainsKey("split_char_list")) //读取此行的自定义分隔符（先按默认分隔符确定协议，然后才按自定义的）
			{
				ArrayList l = v["split_char_list"] as ArrayList;
				split_char_list=new string[l.Count];
				for(int i=0;i<l.Count;i++)
				{
					split_char_list[i]=l[i] as string;
				}
			}
			if (v.ContainsKey("col_n")) col_n = (int)v["col_n"];
			protobj_list = new PD_Str[prot_list.Count];
			for (int i = 0; i < prot_list.Count; i++) //对于每个协议名称，取得子协议的引用，提高引用效率
			{
				protobj_list[i] = prot_list[i] as PD_Str;
			}
		}
		public override int pro(byte[] b, ref int off, int n) //不支持二进制输入
		{
			return 0;
		}
		public void pro_cols(string[] ss) //输入列的列表，用于文本处理
		{
			int col = 0; //处理的列数
			for (int i = 0; i < prot_list.Count && col < ss.Length; i++) //按协议域遍历
			{
				if (protobj_list[i].ref_para.name != "") protobj_list[i].pro_str(ss[col]); //若此列有意义
				col += protobj_list[i].skip_n + 1; //处理下一个列
			}
		}
	}
	public class PD_LineSwitch : PD_Obj //文本行协议分类
	{
		public Encoding cur_encoding = Encoding.UTF8; //默认编码
		//文本协议字典,以文本协议的头为索引，不是name是head

		public string str_buf = ""; //输入行缓存
		public PD_LineSwitch(Dictionary<string, object> v, ProtType t, PD_Obj pd) : base(v, t, pd)
		{
			//PD_Obj初始化的都不用
			if (!v.ContainsKey("prot_list")) return;
			ArrayList list = v["prot_list"] as ArrayList;
			if (v.ContainsKey("encoding")) cur_encoding = Encoding.GetEncoding(v["encoding"] as string);
			prot_dict.Clear();
			prot_list.Clear();
			foreach (var item in list)
			{
				string s = item as string; //直接给了这个协议域的名称，需要是之前定义过的，在结构列表中找。
				if (!p_mcp.struct_dict.ContainsKey(s)) throw new Exception("无顶层协议："+s);
				var tv = p_mcp.struct_dict[s] as JD;
				tv = Tool.jd_clone(tv) as JD;
				PD_LineObj p=gennerate_sub(s, tv) as PD_LineObj; //递归生成子节点，s为名称
				if (p == null) throw new Exception(s + "应为PD_LineObj");

				s = p.head; //也可能是""
				s += "-" + p.col_n.ToString(); //索引名为：$r-2，或者-7
				prot_dict[s] = p;
			}
		}
		public override int pro(byte[] b, ref int off, int n)
		{
			str_buf = cur_encoding.GetString(b, off, n);
			str_buf = str_buf.Trim();
			if (str_buf == "") return 0;
			pro(str_buf);
			return 0;
		}
		public void pro(string s) //直接输入文本的处理函数
		{
			string[] vs = s.Split(", \t".ToCharArray(), StringSplitOptions.None); //有分隔符没数据也算
			if (vs.Length >= 1) //若有数据
			{
				//构造协议名,各协议之间按协议名称区分，协议名称为第一列文本，以$开头，加-列数。若第一列不是$开头，则名称仅为-列数
				string pname = "";
				if (vs[0].StartsWith("$")) pname = vs[0]; //.Substring(1);
				pname += "-" + vs.Length.ToString();
				if (prot_dict.ContainsKey(pname)) //若有这个名字的协议
				{
					var p = prot_dict[pname] as PD_LineObj;
					if (p.split_char_list.Length > 0)//看这个协议是否指定了分隔符（先按默认分隔符确定协议，然后才按自定义的）
					{
						vs = s.Split(p.split_char_list, StringSplitOptions.None);
					}
					p.pro_cols(vs);
				}
			}
		}
	}
}

