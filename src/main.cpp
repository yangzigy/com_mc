#include "common.h"
extern "C"
{
#include "comm_rec.h"
}

///////////////////////////////////////////////////////////////
//对外函数
typedef int (*TxcallBack)(u8 *p,int n); //发送回调函数
extern "C"
{
	int so_ini(TxcallBack pf); //初始化，注册回调函数
	void so_cmd(const char *cc); //发送指令
	const char *so_rx(u8 a); //接收数据函数,返回收到数据的通用格式，若没有收到则为空
	const char *so_poll_100(void); //周期调用,100Hz,返回额外的传感数据
}
TxcallBack tx_cb; //发送回调函数

u8 status_pack_buf[1000];
u8 status_rx_pro(u8 *p,int n);
COMM_SYNC status_pack //状态数据同步
{
	status_pack_buf,
	sizeof(status_pack_buf),
	'\n', 0,
	status_rx_pro,
};

//test
#ifndef SOMAKE
int main(void)
{
	return 0;
}
#endif

string packstr=""; //帧同步数据
u8 status_rx_pro(u8 *p,int n) //接收数据处理
{
	p[n]=0;
	packstr=(char *)p;
	return 0;
}
//////////////////////////////////////////////////////////////////////////////
//函数部分
string retstr="";
int so_ini(TxcallBack pf) //初始化，注册回调函数
{
	tx_cb=pf;
	//slog.log_ini("log.txt");
	return 0;
}
void so_cmd(const char *cc) //发送指令
{
	string s=cc;
	if(s.find("boot")!=string::npos) //如果是想要的指令
	{
		//1、打开文件
		//2、发送数据包
	}
	else //直接发送出去
	{
		s+='\n';
		tx_cb((u8*)s.c_str(),s.size()); //发送
	}
}
const char *so_rx(u8 a) //接收数据函数,返回收到数据的通用格式，若没有收到则为空
{
	rec_sync(a,&status_pack);
	retstr.clear();
	if(status_pack.rec_p==0) //若为0，说明已经组成了一包
	{
		retstr=packstr;
		packstr.clear();
	}
	return retstr.c_str();
}
const char *so_poll_100(void) //周期调用,100Hz,返回额外的传感数据
{
	retstr="";
	return retstr.c_str();
}

