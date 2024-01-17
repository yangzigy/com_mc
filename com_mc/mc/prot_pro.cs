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

namespace com_mc
{
	public partial class PD_Obj : ProtDom //协议对象
	{
		public virtual void reset_state() //复位为待处理状态
		{
			reset_obj();
		}
		public void reset_obj() //复位为待处理状态，obj专用
		{
			int end_n = prot_list.Count;
			//为了提高效率，只复位已经处理的部分。但子类不能这样处理。而且上次执行正确的话，必须全复位(在执行正确的处理时做)
			if (this.GetType() == typeof(PD_Obj) && end_n > pro_ind)
			{
				end_n = pro_ind + 1;
			}
			for (int i = 0; i < end_n; i++)
			{
				var to = prot_list[i] as PD_Obj; //只复位协议组织对象
				if (to != null) to.reset_state();
			}
			pro_ind = 0;
		}
		public override int pro(byte[] b, ref int off, int n) //对于增量输入，需要记录上次处理到哪了
		{
			int pre_off = off; //上次偏移位置
			for (; pro_ind < prot_list.Count; pro_ind++)
			{
				if (n <= 0) return 1; //若已经没有输入数据了，就返回未完成
				int r=prot_list[pro_ind].pro(b, ref off, n); //递归调用
				if (r != 0) return r; //若没完成，或者出错，如实返回
				n -= off - pre_off; //增加了多少字节，总字节数相应减掉
				pre_off = off;
			}
			//reset_state(); //执行正确完成了，还需要清内部的switch。但这样调用会导致调用了派生类的reset
			reset_obj(); //执行正确完成了，还需要清内部的switch。
			return 0;
		}
	}
	public partial class PD_Switch : PD_Obj //选择协议域方式
	{
		public bool pro_cmpl = false; //是否处理完了，要调用后级了
		public override void reset_state()
		{
			pro_cmpl = false;
			base.reset_state(); //递归调用
		}
		public override int pro(byte[] b, ref int off, int n)
		{
			if(pro_cmpl) return prot_map[pro_ind].pro(b, ref off, n); //找到这个协议，调用
			//查看引用的协议域
			var para = ref_dom.ref_para as ParaValue_Val;
			int ti = (int)para.data.du64; //此时是变换以后的
			//查看是否有这个分支
			if(prot_map.ContainsKey(ti)==false) //若不存在这个分支，返回错误
			{
				reset_state();
				return 2;
			}
			//处理
			off += skip_n; //若是从新解析，只需将off给0（给负偏移）
			n -= skip_n; //off之后的长度相应的减少
			pro_cmpl = true;
			pro_ind = ti;
			return prot_map[ti].pro(b, ref off, n); //找到这个协议，调用
		}
	}
	public partial class PD_Loop : PD_Obj //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		public override void reset_state()
		{
			base.reset_state();
			loop_ind = 0;
		}
		public override int pro(byte[] b, ref int off, int n)
		{
			int ti = 0; //重复次数
			if (ref_len != "") //若是指定的
			{
				var para = ref_dom.ref_para as ParaValue_Val;
				ti = para.data.ds32; //此时是变换以后的
			}
			else
			{
				var para = ref_para as ParaValue_Val;
				ti = para.data.ds32; //此时是变换以后的
			}
			//var pr= prot_dict.ToArray()[0].Value; //取得第一个协议域
			int pre_off = off; //上次偏移位置
			for (; loop_ind < ti; loop_ind++)
			{
				int r=base.pro(b, ref off, n); //用PD_Obj的处理方式，loop对象直接包含一系列顺序域
				if (r != 0) return r;
				n -= off - pre_off; //增加了多少字节，总字节数相应减掉
				pre_off = off;
				base.reset_state(); //执行正确完成了一次
			}
			loop_ind = 0; //正确完成了循环
			return 0;
		}
	}
	public partial class MC_Prot //测控参数体系的实现
	{
		//固定数据调用，跟增量是一个处理函数，只是一次给所有的数据
		public void pro_fix(byte[] b,int off,int n,int rootid) //特定协议族处理一帧数据，缓存，偏移，长度（off之后）
		{
			rootid -= 1; //二进制的根节点id是从1开始，0是文本
			//prot_root.pro(b,ref off,n);
			if (rootid>=0 && rootid < prot_root_list.Count) //对指定的根节点进行处理
			{
				var po = prot_root_list[rootid].rootpd;
				po.reset_state(); //复位为待处理状态
				po.pro(b,ref off, n); //调用对应协议族的根节点，跟增量是一个处理函数，只是一次给所有的数据
			}
		}
		//增量数据调用
		public void pro_inc(byte[] b,int off,int n) //所有根节点并行进行协议符合性测试
		{
			//如果所有根节点公用一个缓存，数据在一次处理以后可能剩一半没处理成整帧,下次输入的时候只能继续往下存。
			//所以应该每个根节点设置一个缓存
			for (int i = 0; i < prot_root_list.Count; i++) //对于每一个根实体
			{
				prot_root_list[i].frame_syn_pro(b,off,n); //是否提取接收正确的情况，reset其他？
			}
		}
		//增量输入处理完成后刷新参数，遍历所有需要刷新的参数，刷新到输出参数列表para_dict_out
		public void after_inc(int rootid)
		{
			rootid -= 1; //二进制的根节点id是从1开始，0是文本
			if (rootid < 0 || rootid >= prot_root_list.Count) return; //对指定的根节点进行处理
			var pu = prot_root_list[rootid].para_need_update;
			foreach (var item in pu) //遍历所有需要刷新的参数，刷新到输出参数列表para_dict_out
			{
				if(para_dict_out.ContainsKey(item.name)) para_dict_out[item.name].assign(item); //给参数列表赋值
			}
		}
	}
}

