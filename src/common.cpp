/*
文件名：common.cpp
创建时间：2014-4-16
版本：	V1.1			2019-01-15
版本：	V1.2			2019-08-21

*/
#include "common.h"
#include <stdarg.h>
#include <errno.h>

///////////////////////////////////////////////////////////////////
//1、时间与系统兼容
#ifndef __GNUC__

int strcasecmp(const char *s1, const char *s2)
{
	char c1,c2;
	for (;;)
	{
		c1=toupper(*s1);
		c2=toupper(*s2);
		if (c1<c2)return -1;
		if(c1>c2)return 1;
		if(c1==0)return 0;
		s1++;
		s2++;
	}
}
int strncasecmp(const char *s1, const char *s2,int n)
{
	char c1,c2;
	for (;n--;)
	{
		c1=toupper(*s1);
		c2=toupper(*s2);
		if (c1<c2)return -1;
		if(c1>c2)return 1;
		if(c1==0)return 0;
		s1++;
		s2++;
	}
}
#endif
#if (defined(WIN32) || defined(WIN64))
const char * strp_weekdays[] = 
{ "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"};
const char * strp_monthnames[] = 
{ "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"};
bool strp_atoi(const char * & s, int & result, int low, int high, int offset)
{
	bool worked = false;
	char * end;
	char buf[3]={0};
	memcpy(buf,s,2);
	unsigned long num = strtoul(buf, & end, 10);
	if (num >= (unsigned long)low && num <= (unsigned long)high)
	{
		result = (int)(num + offset);
		//s = end;
		s+=2;
		worked = true;
	}
	return worked;
}
char *strptime(const char *s, const char *format, struct tm *tm)
{
	bool working = true;
	while (working && *format && *s)
	{
		switch (*format)
		{
		case '%':
			{
				++format;
				switch (*format)
				{
				case 'a':
				case 'A': // weekday name
					tm->tm_wday = -1;
					working = false;
					for (size_t i = 0; i < 7; ++ i)
					{
						size_t len = strlen(strp_weekdays[i]);
						if (!strnicmp(strp_weekdays[i], s, len))
						{
							tm->tm_wday = i;
							s += len;
							working = true;
							break;
						}
						else if (!strnicmp(strp_weekdays[i], s, 3))
						{
							tm->tm_wday = i;
							s += 3;
							working = true;
							break;
						}
					}
					break;
				case 'b':
				case 'B':
				case 'h': // month name
					tm->tm_mon = -1;
					working = false;
					for (size_t i = 0; i < 12; ++ i)
					{
						size_t len = strlen(strp_monthnames[i]);
						if (!strnicmp(strp_monthnames[i], s, len))
						{
							tm->tm_mon = i;
							s += len;
							working = true;
							break;
						}
						else if (!strnicmp(strp_monthnames[i], s, 3))
						{
							tm->tm_mon = i;
							s += 3;
							working = true;
							break;
						}
					}
					break;
				case 'd':
				case 'e': // day of month number
					working = strp_atoi(s, tm->tm_mday, 1, 31, 0);
					break;
				case 'D': // %m/%d/%y
					{
						const char * s_save = s;
						working = strp_atoi(s, tm->tm_mon, 1, 12, -1);
						if (working && *s == '/')
						{
							++ s;
							working = strp_atoi(s, tm->tm_mday, 1, 31, -1);
							if (working && *s == '/')
							{
								++ s;
								working = strp_atoi(s, tm->tm_year, 0, 99, 0);
								if (working && tm->tm_year < 69)
									tm->tm_year += 100;
							}
						}
						if (!working)
							s = s_save;
					}
					break;
				case 'H': // hour
					working = strp_atoi(s, tm->tm_hour, 0, 23, 0);
					break;
				case 'I': // hour 12-hour clock
					working = strp_atoi(s, tm->tm_hour, 1, 12, 0);
					break;
				case 'j': // day number of year
					working = strp_atoi(s, tm->tm_yday, 1, 366, -1);
					break;
				case 'm': // month number
					working = strp_atoi(s, tm->tm_mon, 1, 12, -1);
					break;
				case 'M': // minute
					working = strp_atoi(s, tm->tm_min, 0, 59, 0);
					break;
				case 'n': // arbitrary whitespace
				case 't':
					while (isspace((int)*s)) 
						++s;
					break;
				case 'p': // am / pm
					if (!strnicmp(s, "am", 2))
					{ // the hour will be 1 -> 12 maps to 12 am, 1 am .. 11 am, 12 noon 12 pm .. 11 pm
						if (tm->tm_hour == 12) // 12 am == 00 hours
							tm->tm_hour = 0;
					}
					else if (!strnicmp(s, "pm", 2))
					{
						if (tm->tm_hour < 12) // 12 pm == 12 hours
							tm->tm_hour += 12; // 1 pm -> 13 hours, 11 pm -> 23 hours
					}
					else
						working = false;
					break;
				case 'r': // 12 hour clock %I:%M:%S %p
					{
						const char * s_save = s;
						working = strp_atoi(s, tm->tm_hour, 1, 12, 0);
						if (working && *s == ':')
						{
							++ s;
							working = strp_atoi(s, tm->tm_min, 0, 59, 0);
							if (working && *s == ':')
							{
								++ s;
								working = strp_atoi(s, tm->tm_sec, 0, 60, 0);
								if (working && isspace((int)*s))
								{
									++ s;
									while (isspace((int)*s)) 
										++s;
									if (!strnicmp(s, "am", 2))
									{ // the hour will be 1 -> 12 maps to 12 am, 1 am .. 11 am, 12 noon 12 pm .. 11 pm
										if (tm->tm_hour == 12) // 12 am == 00 hours
											tm->tm_hour = 0;
									}
									else if (!strnicmp(s, "pm", 2))
									{
										if (tm->tm_hour < 12) // 12 pm == 12 hours
											tm->tm_hour += 12; // 1 pm -> 13 hours, 11 pm -> 23 hours
									}
									else
										working = false;
								}
							}
						}
						if (!working)
							s = s_save;
					}
					break;
				case 'R': // %H:%M
					{
						const char * s_save = s;
						working = strp_atoi(s, tm->tm_hour, 0, 23, 0);
						if (working && *s == ':')
						{
							++ s;
							working = strp_atoi(s, tm->tm_min, 0, 59, 0);
						}
						if (!working)
							s = s_save;
					}
					break;
				case 'S': // seconds
					working = strp_atoi(s, tm->tm_sec, 0, 60, 0);
					break;
				case 'T': // %H:%M:%S
					{
						const char * s_save = s;
						working = strp_atoi(s, tm->tm_hour, 0, 23, 0);
						if (working && *s == ':')
						{
							++ s;
							working = strp_atoi(s, tm->tm_min, 0, 59, 0);
							if (working && *s == ':')
							{
								++ s;
								working = strp_atoi(s, tm->tm_sec, 0, 60, 0);
							}
						}
						if (!working)
							s = s_save;
					}
					break;
				case 'w': // weekday number 0->6 sunday->saturday
					working = strp_atoi(s, tm->tm_wday, 0, 6, 0);
					break;
				case 'Y': // year
					working = strp_atoi(s, tm->tm_year, 1900, 65535, -1900);
					break;
				case 'y': // 2-digit year
					working = strp_atoi(s, tm->tm_year, 0, 99, 0);
					if (working && tm->tm_year < 69)
						tm->tm_year += 100;
					break;
				case '%': // escaped
					if (*s != '%')
						working = false;
					++s;
					break;
				default:
					working = false;
				}
			}
			break;
		case ' ':
		case '\t':
		case '\r':
		case '\n':
		case '\f':
		case '\v':
			// zero or more whitespaces:
			while (isspace((int)*s))
				++ s;
			break;
		default:
			// match character
			if (*s != *format)
				working = false;
			else
				++s;
			break;
		}
		++format;
	}
	return (working?(char *)s:0);
}
#endif // __GNUC__

u32 com_time_getms(void)
{
	auto now=chrono::system_clock::now();
	return chrono::duration_cast<chrono::milliseconds>(now.time_since_epoch()).count();
}
void delay(int t)
{
	this_thread::sleep_for(chrono::milliseconds(t));
}

/////////////////////////////////////////////////////////////////////////////////
//2、调试与日志
bool is_win_socket_startup=false;
string exepath;//本可执行文件的路径
CLogger slog;//系统的日志对象

void start_program(void)
{
	//加载可执行文件的路径
#ifdef WIN32
	char g_exe_path[MAX_PATH];
	GetModuleFileName(NULL,g_exe_path,MAX_PATH);
	CFilePath path;
	path=g_exe_path;
	exepath=path.path;
#else
	char s[2560];
	char link[2560]={0};
	sprintf(s,"/proc/%d/exe",getpid());
	readlink(s,link,sizeof(link));
	CFilePath tmppath;
	tmppath=link;
	exepath=tmppath.path;
#endif
}
const char *LogLeval[]= //日志中的级别字符
{
	"E","I","W","D",
};
void com_debug_ini(string s) //系统日志对象的初始化
{
	slog.log_ini(s.c_str());
}
///////////////////////////////////////////////////////////////////
//3、字符扩展
//字符串替换功能
string com_replace(string &str,char target,char src)
{
	int i;
	for (i=0;i<str.size();i++)
	{
		if (str[i]==target)
		{
			str[i]=src;
		}
	}
	return str;
}
string com_replace(string str,const char *target,const char *src)
{
	int curPos = 0;
	int pos;
	int len_tar=strlen(target);
	int len_src=strlen(src);
	while((pos = str.find(target, curPos)) != -1)
	{
		str.replace(pos, len_tar, src);      // 一次替换
		curPos = pos + len_src;              // 防止循环替换!!
	}
	return str;
}

void com_strLower(char *s)
{
	while(*s)
	{
		*s=tolower(*s);
		s++;
	}
}
void com_strLower(string &s)
{
	int i;
	for (i=0;i<s.size();i++)
	{
		s[i]=tolower(s[i]);
	}
}

string com_trim(string &s)//去除首尾空格
{
	string::size_type pos1=s.find_first_not_of(' ');
	string::size_type pos2=s.find_last_not_of(' ');
	if (pos1==string::npos)
	{
		pos1=0;
	}
	if (pos2==string::npos)
	{
		pos2=s.size()-1;
	}
	return s.substr(pos1,pos2-pos1+1);
}
//直接格式化成string
string sFormat(const char *format,...)
{
	string s;
	int char_len=10240,len;
	char *buf=new char[char_len];
	va_list args;
	va_start(args,format);
	len=vsnprintf(buf,char_len,format,args);
	while(len<0)
	{
		if (errno!=34)//若不是
		{
			sprintf(buf,"%d:",errno);
			s="error in sFormat, ";
			s+=buf; delete[] buf;
			s+=strerror(errno);
			return s;
		}
		char_len*=2;
		delete[] buf;
		buf=new char[char_len];
		len=vsnprintf(buf,char_len,format,args);
	}
	va_end(args);
	s=buf;
	delete[] buf;
	return s;
}
vector<string> com_split(string &s,const char *c)
{
	vector<string> out;
	int len=strlen(c);
	int off=0,st=0;
	do 
	{
		off=s.find(c,st);
		out.push_back(s.substr(st,off-st));
		st=off+len;
	} while (off>=0);
	return out;
}
//输入字符必须可修改
vector<char*> com_split(char *p,const char *spl)
{
	vector<char*> list;
	int i=strlen(spl);//分隔符字符串的长度
	while((int)(u64)p>1+i)//有效
	{
		list.push_back((char *)p);
		p=strstr(p,spl)+i;
		if ((int)(u64)p<=1+i)
		{
			break;
		}
		p[-i]=0;
	}
	return list;
}
string operator + (const char *a,string &b)
{
	string s;
	s=a;
	return s+b;
}

// 通过文件名设置
bool CFilePath::setName(const char *s)
{
	string ss;
	ss=s;
	return setName(ss);
}
bool CFilePath::setName(string s)
{
	s=com_replace(s,PATH_CHAR_OTHER,PATH_CHAR);
	if (s[s.size()-1]==PATH_CHAR)//检查是否是文件名，若是路径，则正常设置，但返回错误
	{
		setPath(s);
		return false;
	}
	path_name_ext=s;//设置全名
	int i=path_name_ext.find_last_of(PATH_CHAR)+1;
	name_ext=path_name_ext.substr(i,path_name_ext.size()-i);//"a.txt"
	path=path_name_ext.substr(0,i);//"C:\"

	i=name_ext.find_last_of('.');
	if (i<0)
	{
		ext="";
		name=name_ext;
	}
	else
	{
		ext=name_ext.substr(i,name_ext.size()-i);//".txt"
		name=name_ext.substr(0,i);//a
	}
	path_name=path+name;
	return true;
}

// 通过路径设置，需要检查路径有效性
bool CFilePath::setPath(const char *s)
{
	string ss;
	ss=s;
	return setPath(ss);
}
bool CFilePath::setPath(string s)
{
	s=com_replace(s,PATH_CHAR_OTHER,PATH_CHAR);
	if (s[s.size()-1]!=PATH_CHAR)
	{
		path=sFormat("%s%c",s.c_str(),PATH_CHAR);
	}
	else
	{
		path=s;
	}
	path_name_ext="";//全名为空
	name_ext="";
	name="";
	ext="";
	path_name="";
	return true;
}

void CFilePath::operator =(const char * s)
{
	setName(s);
}
void CFilePath::operator =(string s)
{
	setName(s);
}

///////////////////////////////////////////////////////////////////
//4、安全跨平台文件访问
//获取文件长度
s64 get_file_size(FILE* fp)//获取文件长度
{
	s64 r;
	if (fp==NULL) return -1;
	com_fseek( fp, 0L, SEEK_SET );
	com_fseek( fp, 0L, SEEK_END );
	r= com_ftell(fp);
	com_fseek( fp, 0L, SEEK_SET );
	return r;
}
void print_error(const char *name)//输出错误信息
{
	string s=sFormat("%s failed, errno=%d: %s",
			name,errno,strerror(errno));
	DBG_OUT(s.c_str());
}

//安全文件访问
FILE *com_fopen(const char *s,const char *flag)
{
	FILE *f=fopen(s,flag);
	if (f==NULL)
	{
		string st=sFormat("open file %s",s);
		print_error(st.c_str());
	}
	return f;
}

int CComFile::open(string &name,const char *mod)
{
	return open(name.c_str(),mod);
}
int CComFile::open(const char *name,const char *mod)
{
	close();
	filename=name;
	f=com_fopen(name,mod);
	if (f==0)
	{
		return errno;
	}
	len=get_file_size(f);
	return 0;
}
int CComFile::close()
{
	if (f)
	{
		FILE *ft=f;
		f=0;
		return fclose(ft);
	}
	return 1;
}
int CComFile::seek(u64 off)
{
	return com_fseek(f,off,SEEK_SET);
}
s64 CComFile::file_len(void)
{
	len=get_file_size(f);
	return len;
}
s64 CComFile::read(void *p,u64 n)
{
	return fread(p,1,n,f);
}
s64 CComFile::read_safe(void *p,u64 n)//带报错的读操作
{
	s64 real_n=0;//实际字节数
	int try_time=3;//尝试次数
	while(1)
	{
		real_n+=fread(p,1,n-real_n,f);
		if (real_n==n)
		{
			break;
		}
		if(try_time<=0)
		{
			string st=sFormat("read file %s",filename.path_name_ext.c_str());
			print_error(st.c_str());
			break;
		}
		try_time--;
		delay(100);
	}
	return real_n;
}
s64 CComFile::write(const void *p,u64 n)
{
	//return fwrite(p,1,n,f);
	s64 real_n=0;//实际字节数
	int try_time=3;//尝试次数
	while(1)
	{
		real_n+=fwrite(p,1,n-real_n,f);
		if (real_n==n)
		{
			break;
		}
		if(try_time<=0)
		{
			string st=sFormat("write file %s",filename.path_name_ext.c_str());
			print_error(st.c_str());
			break;
		}
		try_time--;
		delay(100);
	}
	return real_n;
}
//离线处理函数,将一个文件以缓冲的方式分批读入
//并调用回调函数进行处理。回调函数中指名当前调用者的数据
void offline_pro(CComFile &file,u64 st,u64 end,u64 bufn,
		int (*fun)(u8 *p,u64 n,u64 offset,void *th),void *obj)
{
	s64 len=end-st;
	if (len<0)
	{
		return ;
	}
	if(file.seek(st)!=0)
	{
		return ;//起始就取不到
	}
	u64 push_pos=com_ftell(file.f);
	u8 *buf=new u8[bufn];
	file.seek(st);
	len=st;//记录从文件头开始的偏移
	while(1)
	{
		s64 rdlen=file.read(buf,bufn);
		int stop=fun(buf,rdlen,len,obj);
		if (rdlen<bufn || stop)//终止条件
		{
			break;
		}
		len+=rdlen;
	}
	delete[] buf;
	file.seek(push_pos);
}
//案例
//int find_cb(u8 *p,u64 n,u64 offset,void *th)
//{
//CCBinTool *pth=(CCBinTool*)th;
//u8 *prst=pth->find_first(p,n);
//if (prst)
//{
//pth->findrstpos.push_back(offset+prst-p);
//return pth->findfirstflag;//返回非零停止处理
//}
//return 0;//返回0为继续处理
//}
//offline_pro(file,st,end,1024*1024*4,find_cb,this);
//读取文本文件
string read_textfile(const char *filename)
{
	ifstream ifs(filename);
	stringstream buffer;  
	buffer << ifs.rdbuf();  
	return buffer.str();
}

#ifdef __GNUC__
string com_popen(const char *scmd)//打开只读管道获取命令输出
{
	FILE *fp=popen(scmd,"r");
	s64 len=0;
	char sbuf;
	vector<char> strvec;
	delay(100);
	while(1)//可能要等一会才能读取到数据
	{
		len=fread(&sbuf,1,1,fp);
		if (len>0)
		{
			strvec.push_back(sbuf);
		}
		else
		{
			break;
		}
	}
	strvec.push_back(0);
	string s="";
	if (strvec.size()>0)
	{
		s=&(strvec[0]);
	}
	pclose(fp);
	return s;
}
#endif
#if (!defined(WIN32) && !defined(WIN64))
#include<sys/types.h>
#include<sys/wait.h>
int _system (const char *command) //不复制内存的调用方式
{
	int pid = 0;
	int status = 0;
	char *argv[4];
	extern char **environ;
	if(NULL==command)
	{
		return -1;
	}
	pid = vfork();
	if(pid<0)
	{
		return -1;
	}
	if(0==pid)
	{             //child process
		argv[0] = "sh";
		argv[1] = "-c";
		argv[2] = (char*)command;
		argv[3] = NULL;
		execve("/bin/sh",argv,environ);// execve() also an implementation of exec() 
		exit(127);
	}
	do //wait for child process to start
	{
		if(waitpid(pid,&status,0)<0)
		{
			//if(errno!=EINTR) { return -1; } else { return status;}
			if(status!=0)
			{
				print_error("");
			}
			return status;
		}
	} while(1);
	return 0;
}
#include <dirent.h>
void list_dir(const char *path,const char *ext,vector<string> &rst) //输入路径名带/，扩展名带. 输出文件名列表，不含全路径
{
	DIR* dp = nullptr;
	struct dirent* dirp = nullptr;
	if((dp = opendir(path)) == nullptr) return ;

	cout<<"asdf"<<endl;
	cout<<ext<<endl;
	while((dirp = readdir(dp)) != nullptr)
	{
		if(dirp->d_type == DT_REG) //若是文件
		{
			string s=dirp->d_name;
			string se="";
			int i=s.find_last_of('.'); //从后找 "."
			if(i>=0) se=s.substr(i,s.size()-i);//".txt"
			cout<<s<<endl;
			cout<<"	"<<se<<endl;
			if(se==ext)
			{
				rst.push_back(s);
			}
		}
	}
	closedir(dp);
	return ;
}
#include <unistd.h> 
#include <sys/stat.h>
int mkdir_1(const char *dirname) //建立1级目录
{  
	int a = access(dirname, F_OK);
	if(a==-1) mkdir(dirname,0755);
	return 0;
}
#else
#include <io.h>
void list_dir(const char *path,const char *ext,vector<string> &rst) //输入路径名带/，输扩展名带. 出文件名列表，不含全路径
{
	string dirname=path;
	dirname+="*";
	dirname+=ext;
	intptr_t handle;
	_finddata_t findData;
	handle = _findfirst(dirname.c_str(), &findData);    // 查找目录中的第一个文件
	if (handle == -1) return ;
	do
	{
		if (findData.attrib & _A_SUBDIR
				&& strcmp(findData.name, ".") == 0
				&& strcmp(findData.name, "..") == 0
		   )    // 是否是子目录并且不为"."或".."
		{
			//cout << findData.name << "\t<dir>\n";
		}
		else
		{
			//cout << findData.name << "\t" << findData.size << endl;
			rst.push_back(findData.name);
		}
	} while (_findnext(handle, &findData) == 0);    // 查找目录中的下一个文件
	_findclose(handle);    // 关闭搜索句柄
	return ;
}
#include <direct.h> //_mkdir函数的头文件
int mkdir_1(const char *dirname) //建立1级目录
{  
	int a = access(dirname, 0);
	if(a==-1) mkdir(dirname);
	return 0;
}
#endif
vector<string> list_dir(const char *path,const char *ext) //输入路径名带/，输出文件名列表，不含全路径
{
	vector<string> rst;
	list_dir(path,ext,rst);
	return rst;
}
int mkdir_p(const char *dirname) //建立多级目录  案例//mkdir_p("./c/b/");
{
	string s=dirname;
	char buf[2]={PATH_CHAR,0};
	s=com_replace(s,PATH_CHAR_OTHER,PATH_CHAR);
	auto dirs=com_split(s,buf);
	if(dirs.size()<=0) return 1;
	s=dirs[0];
	for(auto &it:dirs)
	{
		if(mkdir_1(s.c_str())) return 1;
		s+=buf;
		s+=it;
	}
	return 0;
}
///////////////////////////////////////////////////////////////////
//6、python扩展
#ifdef PYEXT
CPyExt pyext;
void CPyExt::start()
{
	int r=Py_IsInitialized();  //1为已经初始化了
	if (r==0)
	{
		//Py_SetPythonHome(L"C:\\Python35");
		Py_Initialize(); //初始化
		p_main_Module =PyImport_ImportModule("__main__");
		if (!p_main_Module)
		{
			throw "";
		}
	}
}
void CPyExt::end()
{
	Py_Finalize(); //清理
}
int CPyExt::set_string(const char *key,const char *val) //设置变量值，适合变量文本大的情况
{
	PyObject *ps=PyUnicode_DecodeUTF8(val,strlen(val),"ignore");
	if (!ps)
	{
		throw "";
	}
	PyObject_SetAttrString(p_main_Module,key,ps);
	Py_DECREF(ps);
}
string CPyExt::get_string(const char *key) //获取字符型变量值
{
	PyObject *ps=PyObject_GetAttrString(p_main_Module,key); //引用+1
	if (!ps)
	{
		throw "";
	}
	string r=PyUnicode_AsUTF8(ps); //返回指针，调用方不负责释放
	Py_DECREF(ps);
	return r;
}
int CPyExt::eval(const char *p) //执行脚本字符串
{
	return PyRun_SimpleString(p);
}

#endif

