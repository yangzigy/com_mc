using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cslib;
using System.Drawing;

namespace com_mc
{
	static public class Log_Tools //日志数据文件的工具功能
	{
#region hex原始数据转二进制
		static public bool ishex(char c) //判断输入是否是hex字符(只需判断小写字母即可)
		{
			if (char.IsDigit(c)) return true;
			if (c >= 'a' && c <= 'f') return true;
			return false;
		}
		static public void fun_hex2bin() //hex原始数据转二进制
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.*|*.*";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				MessageBox.Show("未选择文件");
				return;
			}
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(ofd.FileName) + "/" +
							Path.GetFileNameWithoutExtension(fin) + "_m"+
							Path.GetExtension(fin);
			//打开输入文件
			StreamReader sw = new StreamReader(fin);
			string text = sw.ReadToEnd(); //输入数据可以不分行
			sw.Close();
			//打开输出文件
			FileStream fsout = new FileStream(fout, FileMode.Create, FileAccess.Write);

			text =text.Replace("0x", " ").ToLower(); //都转换成小写的
			int stat = 0;//状态，0：找字符；1：找第二个字符；
			int st = 0; //起始字符
			byte[] b = new byte[4]; //用于输出的字节数组
			for (int i = 0; i < text.Length; i++)
			{
				string s = "";
				if(stat==0) //若是找第一个字符
				{
					if (ishex(text[i])) //若是hex
					{
						st = i;
						stat = 1;
					}//若不是hex字符，都算空白字符，略过
				}
				else if(stat==1) //若是找第二个字符
				{
					if (ishex(text[i])) //若是hex
					{
						s = text.Substring(st, 2);
					}
					else //若不是hex字符，都算空白字符，就处理单个字符
					{
						s = text.Substring(st, 1);
					}
					stat = 0;
				}
				if(s!="") //若找到了一个hex字节
				{
					b[0] = byte.Parse(s, System.Globalization.NumberStyles.HexNumber);
					fsout.Write(b,0,1);
				}
			}
			if (stat == 1) //若最后一个是单个字符，也要输出
			{
				b[0] = byte.Parse(text.Substring(st, 1), System.Globalization.NumberStyles.HexNumber);
				fsout.Write(b, 0, 1);
			}
			fsout.Close();
		}
		#endregion
#region 二进制原始数据转文本(二进制文件提取)
		static public string[] bin2text_type_tab = new string[] //转换类型的名称
		{ // 0     1     2     3     4    5     6     7
			"u8","u16","u32","u64","s8","s16","s32","s64",
		//    8       9      10       11      12     13       14
			"hex8","hex16","hex32","hex64","float","dobule","bin"
		};
		static public uint[] bin2text_type_len = new uint[] //转换类型的长度
		{
			1,2,4,8,1,2,4,8,1,2,4,8,4,4,1
		};
		static public void fun_bin2text() //二进制原始数据转文本
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.*|*.*";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				MessageBox.Show("未选择文件");
				return;
			}
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(fin) + "/" +
							Path.GetFileNameWithoutExtension(fin) + "_m";

			Com_Dlg cdlg = new Com_Dlg(); //获取输入的对话框
			cdlg.Width= 400;
			Grid subgrid = new Grid(); //控件容器
			cdlg.mgrid.Children.Add(subgrid);
			Grid.SetColumn(subgrid, 0);
			Grid.SetRow(subgrid, 0);
			Grid.SetColumnSpan(subgrid, 2);
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.ColumnDefinitions.Add(new ColumnDefinition());
			subgrid.ColumnDefinitions.Add(new ColumnDefinition());
			cdlg.Height = 260;
			//第一列：描述文本
			Label lb_col = new Label();	lb_col.Content = "列数";
			subgrid.Children.Add(lb_col);
			Grid.SetRow(lb_col, 0); Grid.SetColumn(lb_col,0);

			Label lb_type = new Label();	lb_type.Content = "类型";
			subgrid.Children.Add(lb_type);
			Grid.SetRow(lb_type, 1); Grid.SetColumn(lb_type, 0);

			Label lb_st = new Label(); lb_st.Content = "方阵起始偏移(B)";
			subgrid.Children.Add(lb_st);
			Grid.SetRow(lb_st, 2); Grid.SetColumn(lb_st, 0);

			Label lb_rowdis = new Label(); lb_rowdis.Content = "方阵宽/行间距(0为列宽)";
			subgrid.Children.Add(lb_rowdis);
			Grid.SetRow(lb_rowdis, 3); Grid.SetColumn(lb_rowdis, 0);

			Label lb_row = new Label(); lb_row.Content = "提取行数(0为至尾)";
			subgrid.Children.Add(lb_row);
			Grid.SetRow(lb_row, 4); Grid.SetColumn(lb_row, 0);
			//第二列：输入数值
			TextBox tb_col = new TextBox(); tb_col.Text = "8";
			subgrid.Children.Add(tb_col);
			Grid.SetRow(tb_col, 0); Grid.SetColumn(tb_col, 1);

			ComboBox cb_type = new ComboBox(); cb_type.VerticalContentAlignment = VerticalAlignment.Center;
			for (int i = 0; i < bin2text_type_tab.Length; i++)
			{
				cb_type.Items.Add(bin2text_type_tab[i]);
			}
			cb_type.SelectedIndex = 8; //默认为hex8
			subgrid.Children.Add(cb_type);
			Grid.SetRow(cb_type, 1); Grid.SetColumn(cb_type, 1);

			TextBox tb_st = new TextBox(); tb_st.Text = "0";
			subgrid.Children.Add(tb_st);
			Grid.SetRow(tb_st, 2); Grid.SetColumn(tb_st, 1);

			TextBox tb_rowdis = new TextBox(); tb_rowdis.Text = "0";
			subgrid.Children.Add(tb_rowdis);
			Grid.SetRow(tb_rowdis, 3); Grid.SetColumn(tb_rowdis, 1);

			TextBox tb_row = new TextBox(); tb_row.Text = "0";
			subgrid.Children.Add(tb_row);
			Grid.SetRow(tb_row, 4); Grid.SetColumn(tb_row, 1);

			//弹出对话框，输入
			if (cdlg.ShowDialog() == false) return;

			//打开输入文件
			FileStream fs = new FileStream(fin, FileMode.Open, FileAccess.Read);
			byte[] org_data = new byte[fs.Length];
			fs.Read(org_data, 0, org_data.Length);
			fs.Close();

			//读取输入值
			uint cols = 0; //列数
			int type = cb_type.SelectedIndex; //类型选择直接用序号
			uint st = 0; //起始偏移
			uint row_dis = 0; //行间隔
			uint rows = 0; //行数
			if ((!uint.TryParse(tb_col.Text,out cols)) || cols==0 ||
				(!uint.TryParse(tb_st.Text, out st)) || st>=org_data.Length ||
				(!uint.TryParse(tb_rowdis.Text, out row_dis)) || (!uint.TryParse(tb_row.Text, out rows)))
			{
				MessageBox.Show("参数错误");
				return;
			}
			uint type_len = bin2text_type_len[type]; //类型长度（列元素长度）
			uint w=cols * type_len; //数据列宽
			if (row_dis == 0) row_dis = w; //若指定的行间距为0，行间距为列宽
			uint l_ava = (uint)(org_data.Length - st); //有效数据量
			uint row_ava = (l_ava + row_dis - 1) / row_dis; //有效的行数
			if (rows == 0) rows = row_ava; //如果指定的长度为0，至尾
			else if (rows > row_ava) //若指定了长度但不对
			{
				MessageBox.Show("行数超过有效数据");
				return;
			}
			if(row_dis<w)
			{
				MessageBox.Show("行间距小于列宽");
				return;
			}
			int half_line = (int)(rows * row_dis - l_ava); //若最后一行数据不够
			if (half_line>0 && //输出行数*行间距大于有效数据，说明有半行
				half_line < cols*type_len && //且这半行数据的截止在有效数据以内
				(half_line % type_len)!=0) //半行数据不是元素长度的整数倍
			{
				MessageBox.Show("数据不是类型的整数倍");
				return;
			}
			//若是二进制的
			if (type == 14) //14为二进制
			{
				//修改文件扩展名，为之前的扩展名
				string ext = Path.GetExtension(fin);
				fout += ext;
				FileStream fsout = new FileStream(fout, FileMode.Create, FileAccess.Write);
				uint end = l_ava + (uint)st;
				int ind = (int)st; //当前偏移
				for (int row = 0; row < rows; row++)
				{
					for (int col = 0; col < cols && ind < org_data.Length; col++)
					{
						fsout.WriteByte(org_data[ind]);
						ind++;
					}
					ind += (int)(row_dis - w); //行结束，跳到下一行起始
				}
				fsout.Close();
			}
			else //按文本打开输出文件
			{
				fout += ".txt";
				StreamWriter sw = new StreamWriter(fout);
				string s = "";
				int ind = (int)st; //当前偏移
				for (int row = 0; row < rows; row++)
				{
					for (int col = 0; col < cols && ind < org_data.Length; col++)
					{
						switch (type)
						{
							case 0: s += string.Format("{0} ", org_data[ind]); break; //u8
							case 1: s += string.Format("{0} ", (UInt16)Tool.BytesToStruct(org_data, ind, typeof(UInt16))); break; //u16
							case 2: s += string.Format("{0} ", (UInt32)Tool.BytesToStruct(org_data, ind, typeof(UInt32))); break; //u32
							case 3: s += string.Format("{0} ", (UInt64)Tool.BytesToStruct(org_data, ind, typeof(UInt64))); break; //u64
							case 4: s += string.Format("{0} ", (sbyte)org_data[ind]); break; //s8
							case 5: s += string.Format("{0} ", (Int16)Tool.BytesToStruct(org_data, ind, typeof(Int16))); break; //s16
							case 6: s += string.Format("{0} ", (Int32)Tool.BytesToStruct(org_data, ind, typeof(Int32))); break; //s32
							case 7: s += string.Format("{0} ", (Int64)Tool.BytesToStruct(org_data, ind, typeof(Int64))); break; //s64
							case 8: s += string.Format("{0:X02} ", org_data[ind]); break; //hex8
							case 9: s += string.Format("{0:X04} ", (UInt16)Tool.BytesToStruct(org_data, ind, typeof(UInt16))); break; //hex16
							case 10: s += string.Format("{0:X08} ", (UInt32)Tool.BytesToStruct(org_data, ind, typeof(UInt32))); break; //hex32
							case 11: s += string.Format("{0:X016} ", (UInt64)Tool.BytesToStruct(org_data, ind, typeof(UInt64))); break; //hex64
							case 12: s += string.Format("{0} ", (float)Tool.BytesToStruct(org_data, ind, typeof(float))); break; //float
							case 13: s += string.Format("{0} ", (double)Tool.BytesToStruct(org_data, ind, typeof(double))); break; //double
							default: continue;
						}
						ind += (int)type_len;
					}
					ind += (int)(row_dis - w); //行结束，跳到下一行起始
					sw.WriteLine(s);
					s = "";
				}
				sw.Close();
			}
		}
#endregion
#region 文件合并
		static public void fun_file_merge() //文件合并
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.*|*.*";
			ofd.Title = "选择第1个文件";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				MessageBox.Show("未选择文件");
				return;
			}
			string fin1 = ofd.FileName;
			ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.*|*.*";
			ofd.Title = "选择第2个文件";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				MessageBox.Show("未选择文件");
				return;
			}
			string fin2 = ofd.FileName;
			//输出文件
			string fout = Path.GetDirectoryName(fin1) + "/" +
							Path.GetFileNameWithoutExtension(fin1) + "_m" + 
							Path.GetExtension(fin1);
			//打开第一个文件
			FileStream fs = new FileStream(fin1, FileMode.Open, FileAccess.Read);
			byte[] org_data = new byte[fs.Length];
			fs.Read(org_data, 0, org_data.Length);
			fs.Close();
			//写入输出
			FileStream fsout = new FileStream(fout, FileMode.Create, FileAccess.Write);
			fsout.Write(org_data,0, org_data.Length);
			//打开第二个文件
			fs = new FileStream(fin2, FileMode.Open, FileAccess.Read);
			org_data = new byte[fs.Length];
			fs.Read(org_data, 0, org_data.Length);
			fs.Close();
			fsout.Write(org_data, 0, org_data.Length);
			//关闭输出文件
			fsout.Close();
		}
#endregion
#region cmlog文件合并
		static public void fun_merge_cmlog() //cmlog文件合并（同一文件夹下）
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.cmlog|*.cmlog";
			ofd.Multiselect= true;
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				MessageBox.Show("未选择文件");
				return;
			}
			string[] filelist = ofd.FileNames; //待合并的各个日志文件
			if(filelist.Length <= 1 ) { return; } //如果只有一个文件，不工作
			string fout = Path.GetDirectoryName(ofd.FileName) + "/" + "merge_out.cmlog";
			//合并算法：
			//	每个文件读出一个当前行
			//	找到时间戳最小的那个，写入输出文件
			//	使用过一行的文件，再出一行，重复以上步骤
			List<CMLOG_FILE> cm_files=new List<CMLOG_FILE>(); //各文件
			foreach (var item in filelist)
			{
				CMLOG_FILE f=new CMLOG_FILE();
				
				//读文件
				FileStream fs = new FileStream(item, FileMode.Open, FileAccess.Read);
				f.filedata = new byte[fs.Length];
				fs.Read(f.filedata, 0, f.filedata.Length);
				fs.Close();

				f.cmlog_get_fram(); //取得一帧数据
				cm_files.Add(f);
			}
			//打开输出文件
			FileStream fsout = new FileStream(fout, FileMode.Create, FileAccess.Write);
			//开始转换
			while (true) //
			{
				bool has_data = false; //有数据标志
				int min_ms = int.MaxValue;
				int min_pos = -1;
				for (int i=0;i< cm_files.Count;i++)
				{
					var item = cm_files[i];
					if(item.cur_row!=null) //是否有数据
					{
						has_data = true;
						if(item.cur_row.h.ms < min_ms) //找最小值
						{
							min_ms=item.cur_row.h.ms;
							min_pos = i;
						}
					}
				}
				if (!has_data) { break; }
				if (min_pos < 0) continue;
				//写入文件
				var rowdata = cm_files[min_pos].cur_row;
				var temp = Tool.StructToBytes(rowdata.h);
				fsout.Write(temp, 0, temp.Length); //写入头部
				fsout.Write(rowdata.b, 0, rowdata.b.Length); //写入数据
				//输出的这个文件，继续取得一帧
				cm_files[min_pos].cmlog_get_fram();
			}
			fsout.Close();
		}
		public class CMLOG_FILE //cmlog的文件类，提供文件读取操作
		{
			public CMLOG_ROW cur_row=null; //当前读出的行
			public byte[] filedata = null; //文件中的所有数据
			public int off = 0; //当前读取的位置
			public void cmlog_get_fram() //从cmlog数据中读取一个帧
			{
				if (off > filedata.Length - 8)
				{
					cur_row = null;
					return;
				}

				CMLOG_ROW r = new CMLOG_ROW();
				r.h = (CMLOG_HEAD)Tool.BytesToStruct(filedata, off, typeof(CMLOG_HEAD));
				off += Marshal.SizeOf(r.h);

				r.b = new byte[r.h.len];
				Array.Copy(filedata, off, r.b, 0, r.h.len); //输出数据
				off += r.h.len;

				cur_row= r;
			}
		}
#endregion
#region cmlog信道号修改
		static public void fun_cmlog_vir_change() //cmlog信道号修改
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.*|*.*";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				MessageBox.Show("未选择文件");
				return;
			}
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(fin) + "/" +
							Path.GetFileNameWithoutExtension(fin) + "_m.cmlog";

			List<TextBox> tb_list = new List<TextBox>(); //目标信道号控件列表

			Com_Dlg cdlg = new Com_Dlg(); //获取输入的对话框
			cdlg.Width = 200;
			Grid subgrid = new Grid(); //控件容器
			cdlg.mgrid.Children.Add(subgrid);
			Grid.SetColumn(subgrid, 0);
			Grid.SetRow(subgrid, 0);
			Grid.SetColumnSpan(subgrid, 2);
			subgrid.ColumnDefinitions.Add(new ColumnDefinition());
			subgrid.ColumnDefinitions.Add(new ColumnDefinition()); //2列
			for (int i=0;i<16;i++) //16个虚拟信道,16行
			{
				subgrid.RowDefinitions.Add(new RowDefinition());

				Label lb_col = new Label(); lb_col.Content = string.Format("信道{0}",i);
				subgrid.Children.Add(lb_col);
				Grid.SetRow(lb_col, i); Grid.SetColumn(lb_col, 0);

				TextBox tb_col = new TextBox(); tb_col.Text = string.Format("{0}", i);
				subgrid.Children.Add(tb_col);
				Grid.SetRow(tb_col, i); Grid.SetColumn(tb_col, 1);

				tb_list.Add(tb_col);
			}
			cdlg.Height = 500;

			//弹出对话框，输入
			if (cdlg.ShowDialog() == false) return;

			//构造信道号转换数组
			int[] vir_change = new int[tb_list.Count];
			for(int i=0;i<tb_list.Count;i++)
			{
				vir_change[i] = i;
				int.TryParse(tb_list[i].Text, out vir_change[i]);
			}

			//打开输入文件
			CMLOG_FILE cf = new CMLOG_FILE();
			FileStream fs = new FileStream(fin, FileMode.Open, FileAccess.Read);
			cf.filedata = new byte[fs.Length];
			fs.Read(cf.filedata, 0, cf.filedata.Length);
			fs.Close();

			//打开输出文件
			FileStream fsout = new FileStream(fout, FileMode.Create, FileAccess.Write);

			//遍历读入数据的每一帧
			while (true)
			{
				cf.cmlog_get_fram();
				if (cf.cur_row == null) break;
				var rowdata = cf.cur_row;
				//修改信道号
				rowdata.h.vir = vir_change[rowdata.h.vir];
				//写入文件
				var temp = Tool.StructToBytes(rowdata.h);
				fsout.Write(temp, 0, temp.Length); //写入头部
				fsout.Write(rowdata.b, 0, rowdata.b.Length); //写入数据
			}
			fsout.Close();
		}
#endregion
#region cmlog修改基准时间戳
		static public void fun_cmlog_time() //cmlog修改基准时间戳
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.cmlog|*.cmlog";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				MessageBox.Show("未选择文件");
				return;
			}
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(ofd.FileName) + "/" + Path.GetFileNameWithoutExtension(fin) + "_m.cmlog";

			Com_Dlg cdlg = new Com_Dlg(); //获取输入的对话框
										  //给对话框添加内容：
			Label lb = new Label(); //
			lb.Content = "输入起始时间（ms）";
			lb.FontSize = 14;
			lb.VerticalAlignment = VerticalAlignment.Center;
			TextBox tb = new TextBox();
			tb.Text = "0";
			cdlg.mgrid.Children.Add(lb);
			Grid.SetColumn(lb, 0);
			Grid.SetRow(lb, 0);
			cdlg.mgrid.Children.Add(tb);
			Grid.SetColumn(tb, 1);
			Grid.SetRow(tb, 0);

			if (cdlg.ShowDialog() == false) return;
			int stms = 0;
			bool r = int.TryParse(tb.Text, out stms);
			if (r == false || stms < 0)
			{
				MessageBox.Show("输入非正整数");
				return;
			}
			//拿到了输入输出文件，起始时间
			FileStream fs = new FileStream(fin, FileMode.Open, FileAccess.Read);
			byte[] org_data = new byte[fs.Length];
			fs.Read(org_data, 0, org_data.Length);
			fs.Close();
			//打开输出文件
			FileStream fsout = new FileStream(fout, FileMode.Create, FileAccess.Write);
			//开始转换
			int first_ms = -1;
			for (int i = 0; i < org_data.Length - 6;) //将内存中的数据添加到行列表
			{
				CMLOG_HEAD h = (CMLOG_HEAD)Tool.BytesToStruct(org_data, i, typeof(CMLOG_HEAD));
				int len = h.len; //len这个域是长度
				int ms = h.ms;
				if (first_ms < 0) first_ms = ms; //第一个ms值
				i += Marshal.SizeOf(h);

				h.ms = h.ms - first_ms + stms;

				var temp = Tool.StructToBytes(h);
				fsout.Write(temp, 0, temp.Length); //写入头部
				fsout.Write(org_data, i, len); //写入数据
				i += len;
			}
			fsout.Close();
		}
#endregion
	}
}
