/*
文件名：common.h
创建时间：2014-4-16
版本：	V1.1			2019-01-15
版本：	V1.2			2019-08-21
功能：
	C++常用功能夸平台库，32位、64位的windows与linux
	1、时间功能
	2、调试与日志
	3、字符扩展
	4、安全跨平台文件访问
	5、数值工具
	6、python扩展
*/
#ifndef COMMON_H
#define COMMON_H

#include "main.h"

/////////////////////////////////////////////////////////////////////////////////
//跨平台定义
#if (!defined(WIN32) && !defined(WIN64))
	#define PATH_CHAR	'/'
	#define PATH_CHAR_OTHER	'\\'
	#include <sys/types.h>
	#include <netinet/in.h>
	#include <sys/socket.h>
	#include <arpa/inet.h>
	#include <unistd.h>
	#define close_sock	close
#else
	#define PATH_CHAR	'\\'
	#define PATH_CHAR_OTHER	'/'
	#include <WinSock2.h>
	#include <windows.h>
	#undef UNICODE
	#pragma comment(lib,"ws2_32.lib")
	#define close_sock	closesocket
	typedef int socklen_t;
#endif

#ifndef __GNUC__

#define com_fseek	_fseeki64//32位与64位通用
#define com_ftell	_ftelli64//32位与64位通用

int strncasecmp(const char *s1, const char *s2,int n);
int strcasecmp(const char *s1, const char *s2);

#else

#ifdef __i386__
#define com_fseek	fseek
#define com_ftell	ftell
#else
#define com_fseek	fseeko64
#define com_ftell	ftello64
#endif

#endif

/////////////////////////////////////////////////////////////////////////////////
//1、时间功能
void delay(int ms);
u32 com_time_getms(void);//通用的获得当前ms计数的函数

class CDateTime//时间为系统时间
{
public:
	CDateTime(void)
	{format_str="%Y-%m-%d %H:%M:%S";utc=0;st_time=0;real=0;area_tick=0;}
	CDateTime(const char *p)//设置起始时间的构造函数
	{format_str="%Y-%m-%d %H:%M:%S";utc=0;st_time=0;real=0;set_st(p);area_tick=0;}
	~CDateTime(void){}
	const char *format_str;//格式化字符串

	time_t utc;//临时的秒脉冲(相对)
	time_t st_time;//起始累计时间
	time_t real;//临时秒计数（绝对），相对于系统的起始时间点(目标时区)
	//time_t area_tick;//夸时区的秒数
	int area_tick;//夸时区的秒数

	tm *datetime;//临时年月日
	void set_st(const char *p)//设置起始时间
	{
		st_time=parse(p,format_str);
		utc=0;
	}
	string ToString(const char *p)
	{
		real=st_time+utc + area_tick;
		datetime=localtime(&real);
		char buf[64]={0};
		strftime(buf,sizeof(buf),p,datetime);
		return buf;
	}
	string ToString(void){return ToString(format_str);}
	string Now(const char *p){utc=time(0)-st_time;return ToString(p);}
	string Now(void){return Now(format_str);}
	void update(void){datetime=localtime(&real);}
	void update(time_t d){utc=d;real=utc+st_time + area_tick;update();}
	string utc2str(time_t d,const char *p)
	{
		update(d);
		return ToString(p);
	}
	string utc2str(time_t d){return utc2str(d,format_str);}
	//输入本地时间，输出目标时区的时间
	time_t parse(const char *p,const char *des)
	{
		char *strptime(const char *s, const char *format, struct tm *tm);
		strptime(p,des,&tm_);
		tm_.tm_isdst=-1;
		datetime=&tm_;
		real=mktime(&tm_) - area_tick;
		utc=real-st_time;
		return utc;
	}
private:
	tm tm_;
};

class CSamTime	//测量程序时间的类
{
public:
	CSamTime(void){dt=0;k=0;last_t=0;}
	~CSamTime(void){}
	float k;	//低通滤波的系数，为0~1
	float dt;
	u32 time1;
	u32 time2;
	void start(void)	//开始计时
	{
		time1=com_time_getms();
	}
	float stop(void)	//停止计时
	{
		time2=com_time_getms();
		dt=dt*k + (time2-time1)*(1-k);
		return dt;
	}
	//对一系列点计时
	vector<float> stlist;	//时间差结果列表
	u32 sam_n;	//当前采样的位置
	u32 last_t;
	void sample_ini(int n)	//输入要记多少个值
	{
		sam_n=0;
		stlist.resize(n);
	}
	void sample1(void)	//开始序列计时
	{
		sam_n=1;
		last_t=com_time_getms();
	}
	void sample(void)	//序列中计时
	{
		if (sam_n>stlist.size() || sam_n==0)
		{
			return ;//不工作
		}
		u32 cur_t=com_time_getms();
		//计算当前位置的平均数
		stlist[sam_n-1]=stlist[sam_n-1]*k + (cur_t-last_t)*(1-k);
		last_t=cur_t;
		sam_n++;
	}
	float delta_t(void)//测量任意时间间隔
	{
		u32 tmp=com_time_getms();
		if (last_t==0)
		{
			last_t=tmp;
			return 0;
		}
		dt=dt*k + (tmp-last_t)*(1-k);
		last_t=tmp;
		return dt;
	}
};
/////////////////////////////////////////////////////////////////////////////////
//2、调试与日志
extern string exepath;//本可执行文件的路径
void start_program(void);

//输出分级：
//0：err
//1：inf
//2：war
//3：debug
typedef enum 
{
	TERROR=0,
	TWARNNING,
	TINF,
	TDEBUG
} LogLevalName;
extern const char *LogLeval[]; //日志中的级别字符
class CLogger	//
{
private:
	ofstream log_file;//日志文件
	int sock; //本机的sock
	struct sockaddr_in sendAddr;	//要发送到的地址
	struct sockaddr_in toAddr;	//接收时对方的地址
public:
	CDateTime datetime;//提供当前时间
	void (*udprx_cb)(u8 *d,int n,CLogger* p); //接收指令的回调函数
	void (*log_cb)(const char *,CLogger* p); //发送数据前的回调
	CLogger()
	{
		extern bool is_win_socket_startup;
#ifdef WIN32
		if(!is_win_socket_startup)
		{
			WSADATA wsaData;
			int Ret;
			if ((Ret = WSAStartup(MAKEWORD(2,2), &wsaData)) != 0)
			{
				printf("WSAStartup failed with error %d\n", Ret);
				return ;
			}
			is_win_socket_startup=true;
		}
#endif
		datetime.format_str="%Y%m%d_%H%M%S";
		datetime.area_tick=0;
		leval=3;
		en_stdout=true;
		en_file=false;
		en_udp=false;
		sock=0;
		memset(&sendAddr, 0, sizeof(sendAddr));
		sendAddr.sin_family = AF_INET;
		memset(&toAddr, 0, sizeof(toAddr));
		toAddr.sin_family = AF_INET;
		udprx_cb=0;
		log_cb=0;
	}
	~CLogger()
	{
		extern bool is_win_socket_startup;
		if(sock>0)
		{
			close_sock(sock); //关闭udp端口
		}
#ifdef WIN32
		if(is_win_socket_startup)
		{
			if (WSACleanup() == SOCKET_ERROR)
			{
				printf("WSACleanup failed with error %d\n", WSAGetLastError());
			}
			is_win_socket_startup=false;
		}
#endif
	}
	int leval;//输出等级
	bool en_stdout;//是否向stdout中记录
	bool en_file; //是否向文件中记录
	bool en_udp; //是否向udp中记录
	void log_ini(const char *s)//初始化日志模块,文件方式
	{
		log_file.open(s);
		en_file=true;
	}
	void log_ini(string ip,int rxport,string rmtip,int txport)//udp方式初始化,本地ip,接收端口,远程IP，远程端口
	{
		sock = ::socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
		if (sock < 0)
		{
			log(TERROR,"failed to create socket");
			return ;
		}
		struct sockaddr_in fromAddr;
		memset(&fromAddr, 0, sizeof(fromAddr));
		fromAddr.sin_family = AF_INET;
		fromAddr.sin_addr.s_addr = inet_addr(ip.c_str());
		fromAddr.sin_port = htons(rxport);
		if (::bind(sock, (struct sockaddr *) &fromAddr, sizeof(fromAddr)) < 0)
		{
			log(TERROR,"failed to bind socket");
		}
		sendAddr.sin_addr.s_addr = inet_addr(rmtip.c_str());
		sendAddr.sin_port = htons(txport);
		//开线程
		thread th1([](void *p) //输入字符线程
		{
			CLogger *pp=(CLogger*)p;
			int i;
			while(1)
			{
				int len;
				int addrLen;
				char buf[65536];//接收的缓冲
				addrLen = sizeof(pp->toAddr);
				len = recvfrom(pp->sock, buf, sizeof(buf), 0,
					(struct sockaddr *) &pp->toAddr, (socklen_t*)&addrLen);
				if(len>0 && pp->udprx_cb)
				{
					pp->udprx_cb((unsigned char *)buf,len,(CLogger*)p);
				}
				else if(len<=0)//错误或终止
				{
					break;
				}
			}
		},(void*)this); //end 一般的线程
		th1.detach(); //避免析构
		en_udp=true;
	}
	void log_pass(LogLevalName lev,const char *ps)//记录日志的基础函数
	{
		if(log_cb!=0) log_cb(ps,this);
		if (en_stdout)
		{
			cout<<ps;
			cout.flush();
		}
		if (en_file && lev<=leval)
		{
			log_file<<ps;
			log_file.flush();
		}
		if(en_udp && sock>0)
		{
			::sendto(sock,ps,strlen(ps),0,(struct sockaddr *)&sendAddr,sizeof(sendAddr));
		}
	}
	void log_pass(const char *ps)//记录日志
	{
		log_pass(TINF,ps);
	}
	void log(LogLevalName lev,const char *ps)//记录日志
	{
		string ts=datetime.Now();
		stringstream ss;
		ss<<ts<<":"<<LogLeval[lev]<<"	"<<ps<<endl;
		log_pass(lev,ss.str().c_str());
	}
	CLogger &log(const char *ps)//记录日志
	{
		log(TINF,ps);
		return *this;
	}
	CLogger &log(string &s)//记录日志
	{
		log(s.c_str());
		return *this;
	}
	void log(LogLevalName lev,string &s)//记录日志
	{
		log(lev,s.c_str());
	}
	CLogger &operator<<(const char *s)
	{
		log_pass(s);
		return *this;
	}
	CLogger &operator<<(char *s)
	{
		log_pass(s);
		return *this;
	}
	CLogger &operator<<(string &s)
	{
		log_pass(s.c_str());
		return *this;
	}
	CLogger &operator<<(int a)
	{
		char buf[32]={0};
		sprintf(buf,"%d",a);
		log_pass(buf);
		return *this;
	}
	CLogger &operator<<(ostream& (*_Pfn)(ostream& os)) //这里暂不支持udp方式
	{
		if(en_stdout)
		{
			_Pfn(cout);
		}
		if(en_file && TINF<=leval)
		{
			_Pfn(log_file);
		}
		return *this;
	}
};
extern CLogger slog;//系统的日志对象

#define D(A)	slog<<#A<<"\n";	A
#define DBGL	slog<<__FILE__<<":"<<__LINE__<<"\n"
#define DBG_OUT(x)	slog.log_pass(x)

void com_debug_ini(string s);

/////////////////////////////////////////////////////////////////////////////////
//3、字符扩展
void com_strLower(string &s);
void com_strLower(char *s);
string com_trim(string &s);//去除首尾空格
string com_replace(string str,const char *target,const char *src);
string com_replace(string &str,char target,char src);
string sFormat(const char *format,...);//格式化字符串
vector<string> com_split(string &s,const char *c);
vector<char*> com_split(char *p,const char *spl);
string operator + (const char *a,string &b);

class CFilePath
{
public:
	CFilePath(void){}
	~CFilePath(void){}

	string path;//路径"C:\"
	string name_ext;//文件名"a.txt"
	string ext;//扩展名".txt"
	string name;//基本名"a"
	string path_name_ext;//全名"C:\a.txt"
	string path_name;//路径基本名"C:\a"
	// 通过文件名设置
	bool setName(const char *s);
	bool setName(string s);
	// 通过路径设置，需要检查路径有效性
	bool setPath(const char *s);
	bool setPath(string s);

	void operator =(const char * s);
	void operator =(string s);
};

/////////////////////////////////////////////////////////////////////////////////
//4、安全跨平台文件访问
s64 get_file_size(FILE* fp);//获取文件长度
FILE *com_fopen(const char *s,const char *flag);
//调用shell
void print_error(const char *name);//输出错误信息
#if (!defined(WIN32) && !defined(WIN64))
string com_popen(const char *scmd);//打开只读管道获取命令输出
#endif
int _system(const char *command); //不复制内存的调用方式

class CComFile
{
public:
	CComFile(void){f=0;len=0;}
	~CComFile(void){close();}

	CFilePath filename;
	FILE *f;
	s64 len;
	int open(string &name,const char *mod);
	int open(const char *name,const char *mod);
	int close();
	int seek(u64 off);//从起始开始
	s64 file_len(void);//也更新len
	s64 read(void *p,u64 n);//返回实际读取的字节数
	s64 read_safe(void *p,u64 n);//带报错的读操作
	s64 write(const void *p,u64 n);//返回实际写入的字节数
};
class CLogFile
{
public:
	CComFile flog;
	CDateTime date;
	CFilePath fp; //文件名
	string prefix=""; //前缀
	string suffix=".txt"; //后缀
	u32 pre_utc=0; //上次记录的时间
	u32 restart_T=3600; //重新建立文件的周期(s)
	u32 st_ms=0;  //建立日志时的ms数
	CLogFile()
	{
		date.format_str="%Y%m%d_%H%M%S";
		fp="./";
	}
	virtual void create(string &fname) //建立文件
	{//可重写此函数，实现文件夹建立、文件头
		//QDir().mkpath(fp.path.c_str());
		flog.open(fname.c_str(),"ab");
	}
	virtual void write(const void *p,int n) //写日志
	{
		u32 t=time(0);
		u32 t_h=(t/restart_T)*restart_T; //整小时点
		if(flog.f==0 || pre_utc<t_h) //无文件或到了整小时切换文件
		{
			string fname=fp.path+prefix+date.Now()+suffix;
			this->create(fname);
			st_ms=com_time_getms();
		}
		pre_utc=t;
		flog.write(p,n);
		fflush(flog.f);
	}
	void close(void)
	{
		flog.close();
	}
};
//离线处理函数,将一个文件以缓冲的方式分批读入
//并调用回调函数进行处理。回调函数中指名当前调用者的数据
void offline_pro(CComFile &file,u64 st,u64 end,u64 bufn,
			int (*fun)(u8 *p,u64 n,u64 offset,void *th),void *obj);

//读取文本文件
string read_textfile(const char *filename);
void list_dir(const char *path,const char *ext,vector<string> &rst); //输入路径名带/，输扩展名带. 出文件名列表，不含全路径
int mkdir_1(const char *dirname); //建立1级目录
int mkdir_p(const char *dirname); //建立多级目录  案例//mkdir_p("./c/b/");
/////////////////////////////////////////////////////////////////////////////////
//5、数值工具
template<typename T>
T com_limit(T d,T min,T max) //限制极值
{
	if (d<min)
	{
		d=min;
	}
	else if (d>max)
	{
		d=max;
	}
	return d;
}
template<typename K,typename V>
class mmap : public map<K,V>
{
public:
	V get(K k,V v)
	{
		if (this->find(k)!=this->end())
		{
			return (*this)[k];
		}
		return v;
	}
};
/////////////////////////////////////////////////////////////////////////////////
//6、python扩展
#ifdef PYEXT
#include "Python.h"
class CPyExt //需要单例，且只能用单线程调用
{
public:
	CPyExt(){}
	~CPyExt(){}
	void start(void); //开始Python解释器
	void end(void); //结束Python解释器
	int set_string(const char *key,const char *val); //设置变量值，适合变量文本大的情况
	string get_string(const char *key); //获取字符型变量值
	int eval(const char *p); //执行脚本字符串

	PyObject *p_main_Module;  //主模块
};
extern CPyExt pyext;
#endif

#endif

