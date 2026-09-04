namespace CompilerToy;

/// <summary>
/// 一些定义
/// </summary>
public class Conf
{
	/// <summary>
	/// 变量寄存器个数
	/// </summary>
	public const int MaxPara = 20;
}
/// <summary>
/// 用户自定义类型
/// </summary>
public class UserType
{
	public VarType Type;	
	public readonly string Name;	
	public int ActualSize; //int32
	public readonly bool IsClass;

	private readonly List<RegVar> Member=[]; //偏移值是可用的立即数
	private readonly int Start, End;

	public UserType(List<Token>tok,int start,int end)
	{
		if (tok.Count != 2 || tok[1].Type != TokenType.Letter) throw new Exception("语法错误");
		if (tok[0].Str == "class") IsClass = true;
		Name = tok[1].Str;
		Start = start;End = end;
	}
	/// <summary>
	/// 根据名称返回成员信息。找不到会报错
	/// </summary>
	public RegVar GetMember(string name)
	{
		for (int i = 0; i < Member.Count; i++)
		{
			if (Member[i].Name == name) return Member[i];
		}
		throw new Exception(Name + "未包含" + name + "的定义");
	}
	/// <summary>
	/// 解析类成员
	/// </summary>
	public void Decode(List<Line> line)
	{/*已经得到了ID*/
		int bytes = 0;//总字节数
		for(int i=Start;i<=End;i++)
		{
			VarType vt = PublicTokens.GetVarType(line[i].Tok, 0);
			if (vt.ID == TypeID.None) throw new Exception("未知的类型：" + line[i].Tok[0].Str);
			string name = line[i].Tok[1 + vt.Pdepth * 2].Str;
			int size = vt.Pdepth != 0 ? 4 : PublicTokens.SizeOf(vt.ID);//该成员所占字节数
			int offest = (int)Math.Ceiling((float)bytes / size);
			bytes = (offest + 1) * size;
			
			Member.Add(new(name, vt, RegStat.Locked, offest));
		}
		ActualSize = (int)Math.Ceiling((float)bytes / 4);
	}
}
/// <summary>
/// 将存于寄存器的变量,name,type,state
/// </summary>
public class RegVar(string nam, VarType typ, RegStat state, int off)
{
	public string Name = nam;
	public VarType Type = typ;
	public RegStat State = state;
	public readonly int Offest = off;//寄存器编号

	public void Update(string nam, VarType typ, RegStat state)
	{
		State = state;
		Name = nam;
		Type = typ;
	}
	public override string ToString()
	{
		return Type.ToString() + " - " + Name;
	}
}
/// <summary>
/// 库函数和函数继承此接口
/// </summary>
public interface IFunc
{
	string Name { get; }		//函数名
	List<VarType> Para { get; }	//函数参数
	VarType RetType { get; }	//返回值类型
	int Head { get; }       //函数头部指令位置
}
/// <summary>
/// 全局符号。如函数，全局变量，类型
/// </summary>
public static class PublicTokens
{
	/// <summary>
	/// 公共变量数
	/// </summary>
	public static int PVarCount =>Publicvars.Count;

	private readonly static (string,TypeID)[] typename =
	[
		("int",TypeID.Int),("float", TypeID.Float),("byte", TypeID.Byte),
		("bool", TypeID.Bool),("char",TypeID.Char),
	];// 所有类型的ID。可扩充
	private static List<Function> Funcs = [];
	private static List<IFunc> LibFuncs = [];
	private static List<RegVar> Publicvars = [];
	private static List<(string, int)> C_string = [];
	private static List<UserType> UserTypes = [];
	private static int Strlen = 0;//字符串常量段的长度(int32)
	private static TypeID id = TypeID.Bool + 1;

	/// <summary>
	/// 添加一个用户类型
	/// </summary>
	public static void Add_UserType(UserType typ)
	{
		for (int i = 0; i < UserTypes.Count; i++)
		{
			if (UserTypes[i].Name == typ.Name) throw new Exception("类型重名");
		}
		typ.Type.ID = id;//记录唯一的类型ID
		id++;
		UserTypes.Add(typ);
	}
	/// <summary>
	/// 记录所有用户函数和库函数的定义
	/// </summary>
	public static void Add_UserFunc(List<Function> funcs)
	{
		Funcs = funcs;
		LibFuncs = LibFunction.GetLibFuncs();
	}
	/// <summary>
	/// 记录用户的公共变量定义
	/// </summary>
	public static void Add_UserVar(RegVar vars)
	{
		for (int i = 0; i < Publicvars.Count; i++)
		{
			if (Publicvars[i].Name == vars.Name) throw new Exception("公共变量重名");
		}
		Publicvars.Add(vars);
	}
	/// <summary>
	/// 获取常量字符串的PZ头
	/// </summary>
	public static int Add_String(string str)
	{
		str += str.Length % 2 == 0 ? "\0\0" : '\0';//让长度为偶数，实现内存对齐

		for (int i = 0; i < C_string.Count; i++)
		{
			if (C_string[i].Item1 == str) return C_string[i].Item2;
		}

		int ret = Strlen + PVarCount;//偏移值加上全局变量区大小
		C_string.Add((str, ret));
		Strlen += str.Length / 2;
		return ret;
	}
	/// <summary>
	/// 根据ID返回内存布局信息。找不到会报错
	/// </summary>
	public static UserType GetUserType(TypeID id)
	{
		foreach(UserType ut in UserTypes)
		{
			if(ut.Type.ID == id) return ut;
		}
		throw new Exception("未知的用户类型");
	}
	/// <summary>
	/// 获取常量字符串区的内存映射
	/// </summary>
	public unsafe static int[] ConstString()
	{
		int[] ret = new int[Strlen];

		fixed (int* cp = ret)
		{
			char* p = (char*)cp;
			for (int i = 0; i < C_string.Count; i++)
			{
				for (int j = 0; j < C_string[i].Item1.Length; j++)
				{
					*p++ = C_string[i].Item1[j];
				}
			}
		}//遍历每个字符串，将其合并到int[]
		return ret;
	}
	/// <summary>
	/// 以名称获取公共变量。没找到返回null
	/// </summary>
	public static RegVar? GetVar(string name)
	{
		for (int i = 0; i < Publicvars.Count; i++)
		{
			if (Publicvars[i].Name == name) return Publicvars[i];
		}
		return null;
	}
	/// <summary>
	/// 寻找函数。找不到会报错
	/// </summary>
	public static IFunc GetFunction(string name,List<RegVar>typ)
	{
		for (int j = 0; j < 2; j++)
		{
			bool conv = j != 0;//第一次为false，第二次为true
			for (int i = 0; i < Funcs.Count; i++)
			{
				if (IsTargetFunc(Funcs[i], in name, typ, conv)) return Funcs[i];
			}
			for (int i = 0; i < LibFuncs.Count; i++)
			{
				if (IsTargetFunc(LibFuncs[i], in name, typ, conv)) return LibFuncs[i];
			}
		}
		throw new Exception("没有找到对应函数的重载");
	}
	/// <summary>
	/// 是否为类型标识符。如不是类型会返回Type.None
	/// </summary>
	public static TypeID GetTypeID(string str)
	{
		for (int i = 0; i < typename.Length; i++)
		{
			if (str == typename[i].Item1) return typename[i].Item2;
		}
		for(int i=0;i<UserTypes.Count;i++)
		{
			if (UserTypes[i].Name == str) return UserTypes[i].Type.ID;
		}
		return TypeID.None;
	}
	/// <summary>
	/// 获得<see cref="Token"/>列表所表示的数据类型。不是类型会返回None
	/// </summary>
	public static VarType GetVarType(List<Token>tok,int start)
	{
		TypeID id = GetTypeID(tok[start].Str);
		int pdepth = 0;//记录类型名后的中括号数
		if(id != TypeID.None)
			for (start += 1; tok[start].Str == "[" && tok[start + 1].Str == "]"; start += 2) pdepth++;
		return new VarType(id, pdepth);
	}
	/// <summary>
	/// 类型的大小。如不是类型会报错
	/// </summary>
	public static int SizeOf(TypeID typ)
	{
		switch (typ)
		{
			case TypeID.Float:
			case TypeID.Int:
				return 4;
			case TypeID.Bool:
			case TypeID.Byte:
				return 1;
			case TypeID.Char:
				return 2;
			default:
				for (int i = 0; i < UserTypes.Count; i++)
				{
					if (UserTypes[i].Type.ID == typ) return 4;
				}
				throw new Exception("未知的类型大小");
		}
	}
	/// <summary>
	/// 源类型能否视作目标类型
	/// </summary>
	public static bool ConvertTo(VarType src, VarType dst)
	{
		if (dst.ID == src.ID) return true;
		if (src.Pdepth != 0 || dst.Pdepth != 0) return false;
		if(dst.ID == TypeID.Int)
		{
			if(src.ID == TypeID.Byte || src.ID == TypeID.Char)return true;
		}
		else if(dst.ID == TypeID.Char)
		{
			if(src.ID == TypeID.Byte)return true;
		}
		return false;
	}
	//====辅助函数==============
	/// <summary>
	/// 是否为匹配的重载函数。convert控制是否隐式转换
	/// </summary>
	private static bool IsTargetFunc(IFunc f,in string name, List<RegVar> typ,bool convert)
	{
		if(f.Name != name || f.Para.Count != typ.Count) return false;//同名，同参数长度
		for(int i = 0;i < f.Para.Count;i++)//逐参数对比
		{
			if(convert)//尝试隐式转换
			{
				if (!ConvertTo(typ[i].Type, f.Para[i])) return false;
			}
			else if (!f.Para[i].Equ(typ[i].Type)) return false;
		}
		return true;
	}
}
/// <summary>
/// 表达式编译器
/// </summary>
public class ExpCompiler(List<RegVar> vtype,VarType ret)
{
	/*用于编译函数内的表达式*/
	
	public readonly List<Ins> Ilist = [];

	private readonly List<(IFunc,int)> Calls = [];//存储函数调用
	private readonly List<RegVar> Vtype = vtype;

	private int Depth = 0;
	private readonly Stack<BranchMark> Bmark = [];//存储分支跳转信息

	private readonly VarType Ret = ret;
	private readonly char[] vnam = ['_', 'A'];

	/// <summary>
	/// 根据符号表和优先级表生成栈或指令
	/// </summary>
	public void Compile(List<Token> Tokens, List<int> Pri, string next)
	{
		if(Tokens.Count == 0) return;
		
		VarType typ = PublicTokens.GetVarType(Tokens,0);
		if (typ.ID != TypeID.None)
		{
			CreateVar_user(typ, Tokens[1+typ.Pdepth*2].Str);
			if (Tokens.Count - 2 - typ.Pdepth * 2 == 0) return;
			//语法糖：声明变量的同时初始化
			GenIns(Pri[(1 + typ.Pdepth * 2)..], Tokens[(1 + typ.Pdepth * 2)..], false);
		}//创建变量
		else if (Tokens[0].Str == "if")
		{
			CheckBranch(Tokens,next);

			De_IF(Pri[2..^1], Tokens[2..^1]);   //先编译if表达式
			Bmark.Push(new BranchMark(BranchType.If));
			Bmark.Peek().HeadMark = Ilist.Count - 1;//存语句块头
		}
		else if (Tokens[0].Str == "else")//else  elif
		{
			if (Bmark.Count != Depth + 1 || Bmark.Peek().Mode != BranchType.If) throw new Exception("else语句应位于if语句后");
			if (Tokens.Count == 1) return;//单else不做编译
			
			if (Tokens[1].Str != "if") throw new Exception("语法错误");
			CheckBranch(Tokens[1..], next);

			De_IF(Pri[3..^1], Tokens[3..^1]);		//先编译if表达式
			Bmark.Peek().HeadMark = Ilist.Count - 1;//存语句块头
		}
		else if (Tokens[0].Str == "{")
		{
			Depth++;
		}
		else if (Tokens[0].Str == "}")
		{
			Depth--;
			if(Depth < 0) throw new Exception("大括号数量不匹配");

			BranchMark bbm = Bmark.Peek();

			if (next.StartsWith("else"))
			{
				Ilist.Add(new Ins(InsID.jump, 0, 0, 0));
				Log.Print(InsID.jump, "", "");
				Bmark.Peek().TailMark.Add(Ilist.Count - 1);
			}    //有else。此时添加跳转指令
			else 
			{
				BranchMark bm = Bmark.Pop();
				for (int i = 0; i < bm.TailMark.Count; i++)
				{
					Ilist[bm.TailMark[i]] = new Ins(InsID.jump, Ilist.Count, 0, 0);//更新目标位
					Log.Print("jump[" + bm.TailMark[i] + "] : " + Ilist.Count);
				}
				if(bm.Mode == BranchType.While)//while结尾处也添加跳转指令
				{
					Ilist.Add(new Ins(InsID.jump, bm.HeadStart, 0, 0));
					Log.Print(InsID.jump, bm.HeadStart.ToString(), "");
				}
			}                           //无else。此时结束该分支块
			
			if (bbm.HeadMark != -1)
			{
				Ilist[bbm.HeadMark] = new Ins(InsID.cjmp, Ilist[bbm.HeadMark].Para1, Ilist.Count, 0);//更新目标位
				Log.Print("cjmp[" + bbm.HeadMark + "] : " + Ilist.Count);
				bbm.HeadMark = -1;
			}//若在前面存了头位置，则在此处更改
		}
		else if (Tokens[0].Str == "while")
		{
			CheckBranch(Tokens, next);
			Bmark.Push(new BranchMark(BranchType.While));
			Bmark.Peek().HeadStart = Ilist.Count;//存语句算式头
			De_IF(Pri[2..^1], Tokens[2..^1]);   //先编译while表达式
			
			Bmark.Peek().HeadMark = Ilist.Count - 1;//存语句块头
		}
		else if (Tokens[0].Str == "return")
		{
			if (Ret.Equ(TypeID.None))    //void函数不返回值
			{
				if (Tokens.Count > 1) throw new Exception("void函数不返回数据");
				Ilist.Add(new Ins(InsID.ret0, 0, 0, 0));
				Log.Print(InsID.ret0, string.Empty, string.Empty);
			}
			else                    //非void必须返回值
			{
				if (Tokens.Count == 1) throw new Exception("必须返回一个值");
				RegVar rv = GenPara(Pri[1..], Tokens[1..], Ret);
				if (rv.State == RegStat.Occupied) rv.State = RegStat.Available;
				Ilist.Add(new Ins(InsID.ret, rv.Offest, 0, 0));
				Log.Print(InsID.ret, rv.Name, string.Empty);
			}
		}
		else
		{
			GenIns(Pri, Tokens, false);
			if (Tokens.Count > 0) { throw new Exception("语法错误"); }
		}//表达式
	}
	/// <summary>
	/// 根据Head设置最终跳转地址
	/// </summary>
	public void Link(int Head)
	{
		/*确定call的跳转地址，确定jump的跳转地址*/
		for (int i = 0; i < Ilist.Count; i++)
		{
			if (Ilist[i].ID == InsID.jump)
			{
				Ilist[i] = new Ins(InsID.jump, Ilist[i].Para1 + Head, 0, 0); 
			}
			else if (Ilist[i].ID == InsID.cjmp)
			{
				Ilist[i] = new Ins(InsID.cjmp, Ilist[i].Para1, Ilist[i].Para2 + Head, 0);
			}
		}
		for (int i = 0; i < Calls.Count; i++)
		{
			Ilist[Calls[i].Item2] = new Ins(InsID.call, Calls[i].Item1.Head, 0, 0);
		}
	}
	/// <summary>
	/// 添加函数操作之前的处理
	/// </summary>
	public void AddPreProcess(List<VarType> vt)
	{
		Ilist.Add(new Ins(InsID.pop, 20, 0, 0));//pop R20
		Log.Print(InsID.pop, "R20", string.Empty);
		for (int i = vt.Count - 1; i >= 0; i--)
		{
			Ilist.Add(new Ins(InsID.pop, i, 0, 0));
			Log.Print(InsID.pop,i.ToString(), string.Empty);
		}
		Ilist.Add(new Ins(InsID.stsp, 0, 0, 0));
		Log.Print(InsID.stsp,string.Empty, string.Empty);
	}

	//==编译函数==================================
	private static void CheckBranch(List<Token>Tokens,string next)
	{
		if (Tokens[1].Str != "(" || Tokens[^1].Str != ")") throw new Exception("分支语句后要有小括号");
		if (next != "{") throw new Exception("分支语句后必须有大括号表示的语句块");
	}
	/// <summary>
	/// 生成表达式的字节码指令
	/// </summary>
	private void GenIns(List<int> pri, List<Token> tok,bool genpara)
	{
		int dest = genpara ? 1 : 0;
		while (tok.Count > dest)
		{
			int pr = 0;     //上个运算符的优先级
			int pr_loc = 0; //上个运算符的位置
			bool processed = false;
			for (int i = tok.Count - 1; i >= 0; i--)
			{
				if (tok[i].Type != TokenType.Symbol) continue; 

				if (pri[i] < pr || i <= 1)//对pr或者i本身解构
				{
					processed = true;
					//满足后一个情况且不满足前一个情况
					if (i <= 1 && pri[i] >= pr) pr_loc = i;
					switch (tok[pr_loc].Str)
					{
						case "+":  De_exp(pri, tok, Operators.Add, pr_loc); break;
						case "-":  De_exp(pri, tok, Operators.Sub, pr_loc); break;
						case "*":  De_exp(pri, tok, Operators.Mul, pr_loc); break;
						case "/":  De_exp(pri, tok, Operators.Div, pr_loc); break;
						case "%":  De_exp(pri, tok, Operators.Mod, pr_loc); break;
						case "=":  De_equ(pri, tok, pr_loc);				break;
						case "==": De_exp(pri, tok, Operators.Equ, pr_loc); break;
						case "!=": De_exp(pri, tok, Operators.Nqu, pr_loc); break;
						case ">":  De_exp(pri, tok, Operators.Gtr, pr_loc); break;
						case ">=": De_exp(pri, tok, Operators.Egtr, pr_loc);break;
						case "<":  De_exp(pri, tok, Operators.Smr, pr_loc); break;
						case "<=": De_exp(pri, tok, Operators.Esmr, pr_loc);break;
						case "&&": De_exp(pri, tok, Operators.And, pr_loc); break;
						case "||": De_exp(pri, tok, Operators.Or, pr_loc);	break;
						case "(":  De_bkt(pri, tok, pr_loc);				break;
						case "[":  De_arr(pri, tok, pr_loc - 1);			break;
						case ".":  De_Dot(pri, tok, pr_loc);				break;
						default: { throw new Exception("未知的运算符：" + tok[pr_loc].Str); }
					}
					break;
				}
				pr = pri[i];
				pr_loc = i;
			}
			//如未进行解析，抛出语法错误异常
			if(processed == false) throw new Exception("语法错误");
		}
	}
	/// <summary>
	/// 解析所有数组操作
	/// </summary>
	private void De_arr(List<int> pri, List<Token> tok, int loc)
	{
		/*loc为中括号的前一位*/
		/* 1，new var[var][][] 
		 * 2，arr[var] = ? 
		 * 3，? = arr[var]*/
		if (loc > 0 && (tok[loc - 1].Str == "new" || tok[loc - 1].Str == "stackalloc"))//new obj
		{/*<new/stk> type[var][][] => malloc _a _b 4
		             ^            */
			RegVar vv = GenPara(pri.Slice(loc + 2, 1), tok.Slice(loc + 2, 1), new VarType(TypeID.Int));
			InsID id = tok[loc - 1].Str == "new" ? InsID.malloc : InsID.salloc; //指令码
			TypeID typeid = PublicTokens.GetTypeID(tok[loc].Str);               //分配的类型ID
			int large = PublicTokens.SizeOf(typeid);                            //类型大小
			int depth = 1;                                                      //指针深度

			for (int i = loc + 4; i < tok.Count && tok[i].Str == "[" && tok[i + 1].Str == "]"; i += 2) depth++;
			if (vv.State == RegStat.Occupied) vv.State = RegStat.Available;
			RegVar v2 = CreateVar_temp(new VarType(typeid, depth));//装载结果的变量

			Ilist.Add(new Ins(id, v2.Offest, vv.Offest, large));
			Log.Print(id, v2.Name, vv.Name, large.ToString());

			tok.RemoveRange(loc, depth * 2 + 2);
			pri.RemoveRange(loc, depth * 2 + 2);
			tok[loc - 1] = new Token(TokenType.Letter, v2.Name);
		}//新建变量
		else if (loc + 4 < tok.Count && tok[loc + 4].Str == "=")//左值
		{/*arr[9] = var
		   ^               */
			Ret_TokenType typ = TypeofToken(tok[loc].Str);
			if (typ.Var == null ) throw new Exception("左值应是变量");
			if (tok[loc + 3].Str != "]") throw new Exception("应输入]");
			if (typ.Var.Type.Pdepth <= 0) throw new Exception("无法将[]用于" + typ.Type + "类型的表达式");
			RegVar arr = typ.Var;//数组变量
			if (typ.IsStatic) arr = LoadPvar(arr);

			int index = 0;
			Ret_TokenType typp = TypeofToken(tok[loc + 2].Str);//如索引为常量可进行优化
			if (!PublicTokens.ConvertTo(typp.Type, new(TypeID.Int))) throw new Exception("索引应为整数");
			if (typp.Var == null ) index = ToInstantNum(tok[loc + 2].Str, new VarType(TypeID.Int));

			VarType objtype = new(arr.Type.ID, arr.Type.Pdepth - 1);//解引用一次，depth减1
			RegVar v2 = GenPara(pri.Slice(loc + 5, 1), tok.Slice(loc + 5, 1), objtype);//等号右侧

			int large = arr.Type.Pdepth > 1 ? 4 : PublicTokens.SizeOf(arr.Type.ID);//数组元素大小
			InsID ins;//指令
			if (large == 4) ins = InsID.setp4;
			else if (large == 1) ins = InsID.setp1;
			else if(large == 2) ins = InsID.setp2;
			else throw new Exception("不支持的操作");

			if (typp.Var == null) ins += 2;//变为立即数寻址
			else if (typp.Var.State == RegStat.Occupied) typp.Var.State = RegStat.Available;
			if (v2.State == RegStat.Occupied) v2.State = RegStat.Available;
			if (arr.State == RegStat.Occupied) arr.State = RegStat.Available;

			if (typp.Var != null)
			{
				Ilist.Add(new Ins(ins, arr.Offest, typp.Var.Offest, v2.Offest));
				Log.Print(ins, arr.Name, typp.Var.Name, v2.Name);
			}//局部变量寻址
			else
			{
				Ilist.Add(new Ins(ins, arr.Offest, index, v2.Offest));
				Log.Print(ins, arr.Name, index.ToString(), v2.Name);
			}//立即数寻址

			tok.RemoveRange(loc, 6);
			pri.RemoveRange(loc, 6);
		}//左值
		else//右值
		{/* arr[var] */
			Ret_TokenType typ = TypeofToken(tok[loc].Str);
			if (typ.Var == null ) throw new Exception("左值应是变量");
			if (tok[loc + 3].Str != "]") throw new Exception("应输入]");
			if (typ.Var.Type.Pdepth <= 0) throw new Exception("无法将[]用于" + typ.Type + "类型的表达式");
			if (typ.IsStatic) typ.Var = LoadPvar(typ.Var);

			int index = 0;
			Ret_TokenType typp = TypeofToken(tok[loc + 2].Str);
			if (!PublicTokens.ConvertTo(typp.Type, new(TypeID.Int))) throw new Exception("索引应为整数");
			if (typp.Var == null) index = ToInstantNum(tok[loc + 2].Str, new VarType(TypeID.Int));
			else if (typp.Var.State == RegStat.Occupied) typp.Var.State = RegStat.Available;
			if (typ.Var.State == RegStat.Occupied) typ.Var.State = RegStat.Available;

			RegVar v2 = CreateVar_temp(new(typ.Type.ID, typ.Var.Type.Pdepth - 1));
			int large = typ.Var.Type.Pdepth > 1 ? 4 : PublicTokens.SizeOf(typ.Var.Type.ID);//元素大小
			InsID ins;//指令
			if (large == 4) ins = InsID.getp4;
			else if (large == 1) ins = InsID.getp1;
			else if (large == 2) ins = InsID.getp2;
			else throw new Exception("不支持的操作");

			if (typp.Var == null)
			{
				ins += 2;
				Ilist.Add(new Ins(ins, typ.Var.Offest, index, v2.Offest));
				Log.Print(ins, typ.Var.Name, index.ToString(), v2.Name);
			}//立即数寻址
			else
			{
				Ilist.Add(new Ins(ins, typ.Var.Offest, typp.Var.Offest, v2.Offest));
				Log.Print(ins, typ.Var.Name, typp.Var.Name, v2.Name);
			}//局部变量寻址

			tok.RemoveRange(loc + 1, 3);
			pri.RemoveRange(loc + 1, 3);
			tok[loc] = new Token(TokenType.Letter, v2.Name);
		}//右值
	}
	/// <summary>
	/// 编译小括号。loc位于func名称处
	/// </summary>
	private void De_bkt(List<int> pri, List<Token> tok, int loc)
	{
		TypeID target_id = PublicTokens.GetTypeID(tok[loc + 1].Str);
		TypeID id2 = loc > 0 ? PublicTokens.GetTypeID(tok[loc - 1].Str) : TypeID.None;
		if (target_id != TypeID.None)
		{
			if (tok[loc + 2].Str != ")") throw new Exception("语法错误");
			Ret_TokenType tt = TypeofToken(tok[loc + 3].Str);
			if (tt.Var == null) throw new Exception("无法对常量进行类型转换");
			if (tt.IsStatic) tt.Var = LoadPvar(tt.Var);

			var id = SwitchIns_Convert(target_id, tt.Type);
			
			if (tt.Var.State == RegStat.Occupied) tt.Var.State = RegStat.Available;
			RegVar vv = CreateVar_temp(new VarType(target_id));
			if (id.Item1 != InsID.stop)
			{
				Ilist.Add(new Ins(id.Item1, vv.Offest, tt.Var.Offest, 0));
				Log.Print(id.Item1, vv.Name, tt.Var.Name);
				if(id.Item2 != InsID.stop)
				{
					Ilist.Add(new Ins(id.Item2, vv.Offest, vv.Offest, 0));
					Log.Print(id.Item2, vv.Name, vv.Name);
				}
			}
			else
			{
				Ilist.Add(new Ins(id.Item2, vv.Offest, tt.Var.Offest, 0));
				Log.Print(id.Item2, vv.Name, tt.Var.Name);
			}

			tok.RemoveRange(loc + 1, 3);
			pri.RemoveRange(loc + 1, 3);
			tok[loc] = new Token(TokenType.Letter, vv.Name);
			pri[loc] = -1;

			return;
		}//按照类型转换解析
		if(id2 != TypeID.None)
		{
			if (tok[loc + 1].Str != ")") throw new Exception("构造函数必须是无参的");
			InsID id;
			if (tok[loc - 2].Str == "stackalloc") id = InsID.salloc;
			else if (tok[loc - 2].Str == "new") id = InsID.malloc;
			else throw new Exception("语法错误");

			UserType ut = PublicTokens.GetUserType(id2);
			RegVar one = CreateVar_temp(new VarType(TypeID.Int));
			RegVar rv = CreateVar_temp(ut.Type);//装结果
			if (one.State == RegStat.Occupied) one.State = RegStat.Available;

			Ilist.Add(new Ins(InsID.lod, one.Offest, 1, 0));
			Log.Print(InsID.lod, one.Name, "1");
			Ilist.Add(new Ins(id, rv.Offest, one.Offest, ut.ActualSize*4));
			Log.Print(id, rv.Name, one.Name, (ut.ActualSize*4).ToString());

			pri.RemoveRange(loc - 1, 3);
			tok.RemoveRange(loc - 1, 3);
			tok[loc - 2] = new Token(TokenType.Letter, rv.Name);
			return;
		}//按照构造函数解析
			loc--;
		/* 此函数由GenIns调用。所以括号内为单长token。
		 * func(a,b,c,d) 先解析参数，再寻找函数
		 */
		List<RegVar>vars = [];
		int end = loc+2;
		while (true) 
		{
			if (tok[end-1].Str == ")") { end--;break; }
			if (tok[end].Str == ")") { break; }

			Ret_TokenType typ = TypeofToken(tok[end].Str);
			typ.Var ??= LoadConst(tok, typ.Type, end);//如常量则装载
			if (typ.IsStatic) typ.Var = LoadPvar(typ.Var);
			vars.Add(typ.Var);
			end += 2;
		}
		IFunc func = PublicTokens.GetFunction(tok[loc].Str,vars);//寻找函数，对比定义
		for (int i = 0; i < vars.Count; i++)
		{
			if (vars[i].State == RegStat.Occupied) vars[i].State = RegStat.Available;

			Ilist.Add(new Ins(InsID.push, vars[i].Offest, 0, 0));
			Log.Print(InsID.push, vars[i].Name, string.Empty);
		}//添加push操作
		Ilist.Add(new Ins(InsID.call, 0, 0, 0));
		Log.Print(InsID.call, tok[loc].Str, string.Empty);
		Calls.Add((func, Ilist.Count - 1));//存函数引用，链接时设置跳转地址
		if (!func.RetType.Equ(TypeID.None))//接收返回值
		{
			RegVar vv = CreateVar_temp(func.RetType);
			Ilist.Add(new Ins(InsID.pop, vv.Offest, 0, 0));
			Log.Print(InsID.pop, vv.Name, string.Empty);
			tok[loc] = new Token(TokenType.Letter, vv.Name);
			pri.RemoveRange(loc + 1, end - loc);
			tok.RemoveRange(loc + 1, end - loc);
		}
		else
		{
			pri.RemoveRange(loc, end - loc + 1);
			tok.RemoveRange(loc, end - loc + 1);
		}
	}
	/// <summary>
	/// 编译成员运算符
	/// </summary>
	private void De_Dot(List<int> pri, List<Token> tok, int loc)
	{/*loc位于 . 号上*/
		Ret_TokenType typ1 = TypeofToken(tok[loc - 1].Str);
		if (typ1.Var == null || typ1.Var.Type.Pdepth != 0) throw new Exception("语法错误");
		if (typ1.IsStatic) typ1.Var = LoadPvar(typ1.Var);

		UserType ut = PublicTokens.GetUserType(typ1.Var.Type.ID);
		RegVar member = ut.GetMember(tok[loc + 1].Str);

		InsID id;
		int size = member.Type.Pdepth != 0 ? 4 : PublicTokens.SizeOf(member.Type.ID);
		if (size == 1) id = InsID.setp1c;
		else if (size == 2) id = InsID.setp2c;
		else id = InsID.setp4c;

		if (loc + 2 < tok.Count && tok[loc + 2].Str == "=")
		{/*obj.obj = obj*/
			RegVar v2 = GenPara(pri.Slice(loc + 3, 1), tok.Slice(loc + 3, 1), member.Type);
			if (v2.State == RegStat.Occupied) v2.State = RegStat.Available;
			if (typ1.Var.State == RegStat.Occupied) typ1.Var.State = RegStat.Available;

			Ilist.Add(new Ins(id, typ1.Var.Offest, member.Offest, v2.Offest));
			Log.Print(id, typ1.Var.Name, member.Name, v2.Name);

			tok.RemoveRange(loc - 1, 5);
			pri.RemoveRange(loc - 1, 5);
		}//左值
		else
		{/*obj.obj*/
			id++;
			if (typ1.Var.State == RegStat.Occupied) typ1.Var.State = RegStat.Available;
			RegVar v2 = CreateVar_temp(member.Type);
			Ilist.Add(new Ins(id, typ1.Var.Offest, member.Offest, v2.Offest));
			Log.Print(id, typ1.Var.Name, member.Name, v2.Name);

			tok.RemoveRange(loc, 2);
			pri.RemoveRange(loc, 2);
			tok[loc - 1] = new Token(TokenType.Letter, v2.Name);
		}//右值
	}
	/// <summary>
	/// 编译if/elseif 输入除开if/elif的内容
	/// </summary>
	private void De_IF(List<int> pri, List<Token> tok)
	{
		if (tok.Count == 0) throw new Exception("if语句块内必须有表达式");

		RegVar rv = GenPara(pri, tok, new VarType(TypeID.Bool));
		if(rv.State == RegStat.Occupied) rv.State = RegStat.Available;
		Ilist.Add(new Ins(InsID.cjmp, rv.Offest, 0, 0));
		Log.Print(InsID.cjmp, rv.Name, string.Empty);
	}
	/// <summary>
	/// 处理等号
	/// </summary>
	private void De_equ(List<int> pri, List<Token> tok, int loc)
	{
		//a=b  a=2
		Ret_TokenType re1 = TypeofToken(tok[loc - 1].Str);
		Ret_TokenType re2 = TypeofToken(tok[loc + 1].Str);
		if (re1.Var == null) throw new Exception("不可为常量赋值");
		if (re2.Var == null)//优化：直接装载常量到目标
		{
			if (re1.IsStatic)//使用全局区写入指令
			{/*把常量装载到寄存器，然后拷贝到全局区*/
				RegVar vv = LoadConst(tok, re1.Type, loc + 1);
				
				if (vv.State == RegStat.Occupied) vv.State = RegStat.Available;
				Ilist.Add(new Ins(InsID.setpz, re1.Var.Offest, vv.Offest, 0));//后setpz
				Log.Print(InsID.setpz, re1.Var.Name, vv.Name);
			}
			else
			{
				if (tok[loc + 1].Str.StartsWith('"'))//加载字符串常量
				{
					int lloc = PublicTokens.Add_String(tok[loc + 1].Str[1..^1]);
					Ilist.Add(new Ins(InsID.getpzl, lloc, re1.Var.Offest, 0));
					Log.Print(InsID.getpzl, "[" + lloc + "]" + tok[loc + 1].Str, re1.Var.Name);
				}
				else
				{
					Ilist.Add(new Ins(InsID.lod, re1.Var.Offest, ToInstantNum(tok[loc + 1].Str, re1.Type), 0));
					Log.Print(InsID.lod, tok[loc - 1].Str, tok[loc + 1].Str);
				}
			}
		}
		else            //set
		{
			if (!PublicTokens.ConvertTo(re2.Type, re1.Type))
				throw new Exception("无法把类型" + re2.Type + "隐式转换为" + re1.Type);
			if (re2.Var.State == RegStat.Occupied) re2.Var.State = RegStat.Available;

			Ilist.Add(new Ins(re1.IsStatic ? InsID.setpz : InsID.set, re1.Var.Offest, re2.Var.Offest, 0));
			Log.Print(re1.IsStatic ? InsID.setpz : InsID.set, tok[loc - 1].Str, tok[loc + 1].Str);
		}
		tok.RemoveRange(loc - 1, 3);
		pri.RemoveRange(loc - 1, 3);
	}
	/// <summary>
	/// 处理运算符
	/// </summary>
	private void De_exp(List<int> pri, List<Token> tok,Operators op ,int loc)
	{
		Ret_TokenType typ1 = TypeofToken(tok[loc - 1].Str);//左侧符号的字符串
		Ret_TokenType typ2 = TypeofToken(tok[loc + 1].Str);//右侧符号的字符串
		if (!typ1.Type.Equ(typ2.Type) &&
			!PublicTokens.ConvertTo(typ1.Type,new(TypeID.Int)) ||
			!PublicTokens.ConvertTo(typ2.Type, new(TypeID.Int))) 
			throw new Exception("运算符"+op+"无法用于"+typ1.Type + "与"+typ2.Type);

		if (typ1.Var == null && typ2.Var == null)
		{
			tok[loc - 1] = new Token(TokenType.Digit, op.Optimize(tok[loc - 1].Str, tok[loc + 1].Str));
			tok.RemoveRange(loc, 2);
			pri.RemoveRange(loc, 2);
			return;
		}//全常量时优化表达式
		typ1.Var ??= LoadConst(tok, typ1.Type, loc - 1);//将左侧常量装载
		typ2.Var ??= LoadConst(tok, typ2.Type, loc + 1);//将右侧常量装载
		if (typ1.IsStatic) typ1.Var = LoadPvar(typ1.Var);
		if (typ2.IsStatic) typ2.Var = LoadPvar(typ2.Var);
		/*现在为全变量*/

		if (typ1.Var.State == RegStat.Occupied) typ1.Var.State = RegStat.Available;
		if (typ2.Var.State == RegStat.Occupied) typ2.Var.State = RegStat.Available;
		RegVar vv = CreateVar_temp(new VarType(op.GetRes(typ1.Type)));
		Ilist.Add(new Ins(op.GetIns(typ1.Type), vv.Offest, typ1.Var.Offest, typ2.Var.Offest));
		Log.Print(op.GetIns(typ1.Type), vv.Name, typ1.Var.Name, typ2.Var.Name);

		//更改符号表
		tok[loc-1] = new Token(TokenType.Letter, vv.Name);
		tok.RemoveRange(loc, 2);
		pri.RemoveRange(loc, 2);

	}
	
	//==辅助函数：指令类型选择=====================

	private static (InsID, InsID) SwitchIns_Convert(TypeID t,VarType s)
	{
		if (s.Pdepth > 0 ) throw new Exception("无法转换指针类型");
		if (s.ID == t || t == TypeID.Int && s.ID == TypeID.Byte) throw new Exception("多余的类型转换");


		InsID step1 = s.ID == TypeID.Float ? InsID.f2i : InsID.stop;
		InsID step2 = InsID.stop;
		switch(t)
		{
			case TypeID.Float:step2 = InsID.i2f; break;
			case TypeID.Int:break;
			case TypeID.Byte:step2 = InsID.i2b;break;
			default: throw new Exception("无法转换为" + t + "类型");
		}
		return (step1, step2);
	}
	//==辅助函数==================================
	
	/// <summary>
	/// 以参数为结果解析源码。会加载全局变量。可能返回用户变量
	/// </summary>
	private RegVar GenPara(List<int> pri, List<Token> tok, VarType type)
	{
		GenIns(pri, tok, true);//若嵌套，先解构

		Ret_TokenType typ1 = TypeofToken(tok[0].Str);
		if(typ1.Var != null)
		{
			if (!typ1.Type.Equ(type)) throw new Exception("参数不是" + type + "类型");
			if (typ1.IsStatic) typ1.Var = LoadPvar(typ1.Var);
			return typ1.Var;//为变量则直接返回
		}

		return LoadConst(tok, type, 0);//为常量则装载后返回
	}
	/// <summary>
	/// 装载常量并更改符号表，附带隐式类型转换
	/// </summary>
	private RegVar LoadConst(List<Token> tok, VarType type, int loc)
	{
		RegVar ret = CreateVar_temp(type);

		if (tok[loc].Str.StartsWith('"'))
		{
			if (!type.Equ(new VarType(TypeID.Char, 1))) throw new Exception("语法错误");

			int lloc = PublicTokens.Add_String(tok[loc].Str[1..^1]);
			Ilist.Add(new Ins(InsID.getpzl, lloc, ret.Offest, 0));
			Log.Print(InsID.getpzl, "[" + lloc + "]" + tok[loc].Str, ret.Name);
		}//加载字符串常量
		else
		{
			Ilist.Add(new Ins(InsID.lod, ret.Offest, ToInstantNum(tok[loc].Str, type), 0));
			Log.Print(InsID.lod, ret.Name, tok[loc].Str);
		}//加载立即数
		tok[loc] = new Token(TokenType.Letter, ret.Name);
		return ret;
	}
	/// <summary>
	/// 把公共变量加载到寄存器
	/// </summary>
	private RegVar LoadPvar(RegVar vv)
	{
		RegVar ret = CreateVar_temp(vv.Type);
		Ilist.Add(new Ins(InsID.getpz, vv.Offest, ret.Offest, 0));
		Log.Print(InsID.getpz, vv.Name, ret.Name);
		return ret;
	}
	/// <summary>
	/// 创建用户变量
	/// </summary>
	public RegVar CreateVar_user(VarType typ,string name)
	{
		if(name == "int" || name == "bool") throw new Exception("不能以关键字命名变量");
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].Name != name) continue;
			throw new Exception("变量重名");
		}//先检查重名
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].State != RegStat.UnUsed) continue;

			Vtype[i].Update(name, typ, RegStat.Locked);
			return Vtype[i];
		}//然后分配UnUsed寄存器
		throw new Exception("寄存器溢出");
	}
	/// <summary>
	/// 为临时变量分配寄存器
	/// </summary>
	private RegVar CreateVar_temp(VarType typ)
	{
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].State != RegStat.Available) continue;
			Vtype[i].Update(Vtype[i].Name, typ, RegStat.Occupied);
			return Vtype[i];
		}//尝试复用Available寄存器
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].State != RegStat.UnUsed) continue;

			Vtype[i].Update(new string(vnam), typ, RegStat.Occupied);
			vnam[1]++;
			return Vtype[i];
		}//使用UnUsed寄存器
		throw new Exception("寄存器溢出");
	}
	/// <summary>
	/// 获取符号的类型以及是否为常量
	/// </summary>
	private Ret_TokenType TypeofToken(string nam)
	{
		if (char.IsDigit(nam[0]))
		{
			if (nam.Contains('.')) return new Ret_TokenType(null, new VarType(TypeID.Float),false);
			return new Ret_TokenType(null, new VarType(TypeID.Int),false);
		}//以数字常量计
		else if(nam == "true" || nam == "false")
		{
			return new Ret_TokenType(null, new VarType(TypeID.Bool),false);
		}//以布尔常量计
		else if(nam.StartsWith('"'))
		{
			return new Ret_TokenType(null, new VarType(TypeID.Char,1),false);
		}//以字符数组常量计
		else
		{
			for (int i = 0; i < Vtype.Count; i++)
			{
				if (Vtype[i].Name != nam) continue;
				return new Ret_TokenType(Vtype[i], Vtype[i].Type, false);
			}//找私有变量
			RegVar? vv = PublicTokens.GetVar(nam);   //找公共变量
			if (vv != null)return new Ret_TokenType(vv, vv.Type, true);
			throw new Exception("未知的变量名：" + nam);
		}//以变量计
	}
	/// <summary>
	/// 把非指针的常量符号转为立即数。附带隐式类型转换
	/// </summary>
	private static unsafe int ToInstantNum(string nam,VarType typ)
	{
		if (typ.Pdepth != 0) throw new Exception("无法把常量赋值给指针");
		switch(typ.ID)
		{
			case TypeID.Byte: return Convert.ToByte(nam);
			case TypeID.Int:  return Convert.ToInt32(nam);
			case TypeID.Bool:
					if (nam == "true")  return 1;
					if (nam == "false")  return 0;
					throw new Exception("常量 " + nam + "不是布尔类型");
			case TypeID.Float:
					float ff = Convert.ToSingle(nam);
					return *(int*)&ff;
			case TypeID.Char: return Convert.ToInt16(nam);
			default:throw new Exception("无法把常量"+nam+"解析为"+typ);
		}
	}
}

public class Compiler
{
	private readonly List<Line> Code;		//分成行的程序
	private readonly List<Function> Funcs;  //程序中的函数

	private ExpCompiler? Init_Sector;

	public Compiler()
	{
		Code = [];
		Funcs = [];
		PublicTokens.Add_UserFunc(Funcs);
	}
	/// <summary>
	/// 预处理代码。转语法糖，生成标准行
	/// </summary>
	public void PreProcess(string sourcecode)
	{
		DateTime dateTime = DateTime.Now;
		
		Code.AddRange(SynaxAnalyzer.Split2(sourcecode));
		Console.WriteLine("预处理用时：" + (DateTime.Now - dateTime).TotalMilliseconds + " 毫秒");
	}
	/// <summary>
	/// 编译生成变量，指令，常量池
	/// </summary>
	public void Compile()
	{/*已经处理了小括号*/

		DateTime dateTime = DateTime.Now;
		int depth = 0;
		int start = 0;//缓存函数开头行
		bool isclass = false;
		List<Line> publicvars = [];
		List<(int, int)> fregion = [];//缓存函数范围
		List<UserType> usertypes = [];
		for(int i=0;i<Code.Count;i++)
		{
			if (Code[i].Tok[0].Str == "{") depth++;
			else if (Code[i].Tok[0].Str == "}")
			{
				depth--;
				if (depth < 0) { throw new Exception("正大括号数量少于反大括号"); }
				else if (depth == 0)
				{
					if (!isclass) fregion.Add((start, i));
					else usertypes.Add(new UserType(Code[start].Tok, start + 2, i - 1));
					
					start = i + 1;
				}
			}
			else if (depth == 0)
			{
				if (Code[i].Tok[0].Str == "class") { start = i; isclass = true; continue; }

				isclass = false;
				int ll = 1;
				while (Code[i].Tok[ll].Str == "[") ll += 2;
				ll++;
				if (ll < Code[i].Tok.Count && Code[i].Tok[ll].Str == "(") start = i;//函数
				else publicvars.Add(Code[i]);//变量
			}
		}//分出函数，用户类型与全局变量
		if(depth != 0) throw new Exception("大括号数量不匹配");

		for(int i=0;i<usertypes.Count;i++)
		{
			PublicTokens.Add_UserType(usertypes[i]);
		}//先注册用户类型，以应对类型互相引用的情况
		for (int i = 0; i < usertypes.Count; i++)
		{
			usertypes[i].Decode(Code);
		}//后编译用户类型
		Console.WriteLine("全局变量表");
		for(int i=0;i<publicvars.Count;i++)
		{
			VarType vt = PublicTokens.GetVarType(publicvars[i].Tok, 0);
			string nam = publicvars[i].Tok[1 + vt.Pdepth * 2].Str;
			PublicTokens.Add_UserVar(new RegVar(nam, vt, RegStat.Locked, i));
			Console.WriteLine("\t"+vt + "\t" + nam + "\t[" + i + ']');

			publicvars[i]=new Line(publicvars[i].Number,
								   publicvars[i].Tok[(1 + vt.Pdepth * 2)..],
								   publicvars[i].Pri[(1 + vt.Pdepth * 2)..]);
		}//添加全局变量定义
		GenInitSector(publicvars);//引导段
		Console.WriteLine();
		for (int i = 0; i < fregion.Count; i++)
		{
			Funcs.Add(new Function(Code[fregion[i].Item1].Tok, fregion[i].Item1 + 2, fregion[i].Item2 - 1));
			Funcs[^1].Compile(Code);
			Out(Funcs[^1]);
		}//遍历，定义并编译每个函数
		
		Console.WriteLine("编译用时：" + (DateTime.Now - dateTime).TotalMilliseconds + " 毫秒");
	}
	/// <summary>
	/// 生成引导代码段
	/// </summary>
	private void GenInitSector(List<Line> list)
	{
		Console.WriteLine("变量初始化段");
		List<RegVar> RegVars = [];
		for (int i = 0; i < Conf.MaxPara; i++)
		{
			RegVars.Add(new RegVar(string.Empty, new VarType(TypeID.None), RegStat.UnUsed, i));
		}//初始化寄存器表
		Init_Sector = new ExpCompiler(RegVars, new VarType(TypeID.None));
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].Tok.Count == 1) continue;
			Init_Sector.Compile(list[i].Tok, list[i].Pri, string.Empty);
		}
		
		Init_Sector.Ilist.Add(new Ins(InsID.call, 0, 0, 0));//调用main函数
		Init_Sector.Ilist.Add(new Ins(InsID.stop, 0, 0, 0));//从main返回后结束程序
		Log.Out();
	}
	/// <summary>
	/// 链接函数。确定跳转指令地址
	/// </summary>
	public Program Link()
	{
		if (Init_Sector == null) throw new Exception();
		/*链接产物是指令表。调用深度，栈深度
		  先生成引导代码段，然后依序排列指令。*/
		DateTime tim = DateTime.Now;
		List<Ins> Ilist = [];
		int Ilistlen = Init_Sector.Ilist.Count;
		int clen = 20;
		int stacklen = 0;

		for (int i = 0; i < Funcs.Count; i++)
		{
			Funcs[i].Head = Ilistlen;
			Ilistlen += Funcs[i].Ilength;
			if (Funcs[i].Name == "Main")
			{
				Init_Sector.Ilist[^2] = new Ins(InsID.call, Funcs[i].Head, 0, 0);//设置main函数调用
				if (!Funcs[i].RetType.Equ(TypeID.None))throw new Exception("Main函数不能有返回值");
				if (Funcs[i].Para.Count > 0) throw new Exception("Main函数应是无参数的");
			}
		}//仅设置函数地址
		Init_Sector.Link(0);
		Ilist.AddRange(Init_Sector.Ilist);//引导代码段

		if (Ilist[Init_Sector.Ilist.Count-2].Para1 == 0) throw new Exception("没有找到Main函数");
		for (int i = 0; i < Funcs.Count; i++)
		{
			Ilist.AddRange(Funcs[i].Link());
			stacklen += Funcs[i].Para.Count;
		}//链接函数并把代码拷贝到指令表
		if (Ilist.Count == Init_Sector.Ilist.Count) throw new Exception("没有生成任何指令");
		stacklen *= 3;//栈深度以4字节计
		stacklen += 200;
		Console.WriteLine("链接用时："+(DateTime.Now - tim).TotalMilliseconds+" 毫秒");

		return new Program(stacklen, clen, Ilist.ToArray(), PublicTokens.PVarCount, PublicTokens.ConstString());
	}
	
	/// <summary>
	/// 用于debug
	/// </summary>
	private static void Out(Function f)
	{
		Console.Write("<Function> "+f.RetType.ToString() + ' ' + f.Name + '(');
		for (int i = 0; i < f.Para.Count; i++) Console.Write(f.Para[i]+" " + f.RegVars[i].Name + " ,");
		Console.WriteLine(')');
		Console.WriteLine("<寄存器表>");
		List<RegVar> Vtype =f.RegVars;
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].State == RegStat.UnUsed) continue;
			Console.WriteLine("\t"+Vtype[i].Type.ToString() + '\t' + Vtype[i].Name + "\t[" + Vtype[i].Offest + "]");
		}
		Console.WriteLine("<指令表>");
		Log.Out();
		Console.WriteLine();
	}
}

/// <summary>
/// 表示函数
/// </summary>
public class Function : IFunc
{
	public VarType RetType { get; }		//返回类型
	public List<VarType> Para { get; }	//参数类型
	public string Name { get; }         //函数名
	public int Head { get; set; }		//函数头部位置。用于链接
	public int Ilength => Excomp.Ilist.Count;//指令长度

	public readonly List<RegVar> RegVars;	//寄存器表。用于编译

	private readonly ExpCompiler Excomp;        //表达式编译器。每个函数配一个
	private readonly int Start;
	private readonly int End;
	public Function(List<Token>tok,int start,int end)
	{
		/* void Func ( int a , int b , int c ) */
		/* Person[][] Func ( int [] a, int [] b ) */
		int now = 0;
		Start = start;End = end;
		RegVars = [];
		Para = [];
		for(int i=0;i<Conf.MaxPara;i++)
		{
			RegVars.Add(new RegVar(string.Empty, new VarType(TypeID.None), RegStat.UnUsed, i));
		}//初始化寄存器表
		RetType = PublicTokens.GetVarType(tok, 0);//解析返回值类型
		now += 1 + RetType.Pdepth * 2;
		Excomp = new ExpCompiler(RegVars,RetType);

		if (RetType.Equ(TypeID.None) && tok[0].Str != "void") throw new Exception("没有找到类型：" + tok[0].Str);
		Name = tok[now].Str;
		if (tok[now+1].Str != "(" || tok[^1].Str != ")") throw new Exception("函数定义错误：小括号");
		//解析参数表
		if (tok.Count - now == 3) return;//无参函数
		for (int i = now+2; i < tok.Count;)
		{
			VarType vt = PublicTokens.GetVarType(tok, i);
			if (vt.ID == TypeID.None) throw new Exception("未知的参数类型：" + tok[i].Str);
			i += 1+vt.Pdepth * 2;
			Excomp.CreateVar_user(vt, tok[i].Str);
			Para.Add(vt);
			if (i!= tok.Count-2 && tok[i + 1].Str != ",") throw new Exception("函数参数要以逗号隔开");
			if (i == tok.Count - 2) break;
			i += 2;
		}
	}
	/// <summary>
	/// 外部循环调用。逐行编译
	/// </summary>
	public void Compile(List<Line> Code)
	{
		Excomp.AddPreProcess(Para);
		for (int j = Start; j <= End; j++)
		{
			//try
			//{
			string next = j == Code.Count - 1 ? string.Empty : Code[j + 1].Tok[0].Str;
			Excomp.Compile(Code[j].Tok, Code[j].Pri, next);
			//}
			//catch (Exception e) { throw new Exception("Line " + Code[j].Number + " " + e.Message); }
		}
		if (Excomp.Ilist[^1].ID != InsID.ret && Excomp.Ilist[^1].ID != InsID.ret0)
		{
			if (RetType.Equ(TypeID.None))
			{ 
				Excomp.Ilist.Add(new Ins(InsID.ret0, 0, 0, 0));
				Log.Print(InsID.ret0, string.Empty, string.Empty);
			}
			else throw new Exception("函数必须返回一个数据");
		}
	}
	/// <summary>
	/// 链接
	/// </summary>
	public List<Ins> Link()
	{
		Excomp.Link(Head);
		return Excomp.Ilist;
	}
}

//汇编编译器的部分函数
/*
/// <summary>
/// 编译器工具函数
/// </summary>
public class CompilerFunc
{
	public int Stacklen = 0;
	public List<int> Voffest;

	private readonly List<Var> Vtype;
	private readonly string[] Code;
	private readonly string[] Kword =
	{
		"endl"
	};

	public CompilerFunc(Compiler comp)
	{
		Voffest = [];
		//Clist = comp.Clist;
		Vtype = comp.Vtype;
		Code = comp.Code;
	}
	/// <summary>
	/// 在栈中创建变量，同步记录偏移
	/// </summary>
	public void CreateVar(int nameindex, VarType type)
	{
		CheckNewVar(nameindex);
		Vtype.Add(new Var(Code[nameindex], type, Stacklen, VarStat.Locked));
		Voffest.Add(Stacklen);
		Stacklen += SizeOf(type);
	}
	/// <summary>
	/// 变量地址，附带检查
	/// </summary>
	public int GetVarIndex(int index)
	{
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].Name == Code[index])
			{
				return Voffest[i];
			}
		}
		throw new Exception("没找到变量名");
	}
	public int SizeOf(VarType typ)
	{
		switch (typ)
		{
			case VarType.Int:
				{
					return 4;
				}
		}
		throw new Exception("未知的类型大小");
	}
	/// <summary>
	/// 是否可转int？
	/// </summary>
	public bool IsInt(int index)
	{
		for (int i = 0; i < Code[index].Length; i++)
		{
			if (Code[index][i] < '0' || Code[index][i] > '9')
			{
				return false;
			}
		}
		return true;
	}
	/// <summary>
	/// valid new name
	/// </summary>
	private void CheckNewVar(int index)
	{
		for (int i = 0; i < Kword.Length; i++)
		{
			if (Kword[i] == Code[index])
			{
				throw new Exception("不能用关键字命名");
			}
		}
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].Name == Code[index])
			{
				throw new Exception("存在相同的变量名");
			}
		}
	}
	/// <summary>
	/// 检查是否为endl
	/// </summary>
	public void CheckEnd(int index)
	{
		if (Code[index] != "endl")
		{
			throw new Exception("出现了多余的命令");
		}
	}

}
*/
/*
	/// <summary>
	/// 生成可执行文件
	/// </summary>
	public Program GenHex()
	{
		return new Program(CompFunc.Stacklen, Ilist.ToArray());
	}
	/// <summary>
	/// 编译一行
	/// </summary>
	private void CompileLine()
	{
		switch (Code[Now])
		{
			case "stop":
				{
					//stop endl
					CompFunc.CheckEnd(Now + 1);//end of the line
					Ilist.Add(new Ins(InsID.stop, 0, 0, 0));
					Now += 2;
					break;
				}
			case "int":
				{
					// int a endl
					CompFunc.CreateVar(Now + 1,VarType.Int);
					CompFunc.CheckEnd(Now + 2);//end of the line
					Now += 3;
					break;
				}
			case "lodi":
				{
					//lodi a 500 endl
					int ind = CompFunc.GetVarIndex(Now + 1);//是否为变量名？
					Ilist.Add(new Ins(InsID.lodi, ind, Convert.ToInt32(Code[Now + 2]), 0));
					CompFunc.CheckEnd(Now + 3);
					Now += 4;
					break;
				}
			case "seti":
				{
					//seti a b endl
					int ind = CompFunc.GetVarIndex(Now + 1);//是否为变量名？
					int ind2 = CompFunc.GetVarIndex(Now + 2);
					CompFunc.CheckEnd(Now + 3);

					Ilist.Add(new Ins(InsID.seti, ind, ind2, 0));
					Now += 4;
					break;
				}
			case "addi":
				{
					//addi a b endl
					int ind = CompFunc.GetVarIndex(Now + 1);//是否为变量名？
					int ind2 = CompFunc.GetVarIndex(Now + 2);
					CompFunc.CheckEnd(Now + 3);

					Ilist.Add(new Ins(InsID.addi, ind, ind2, 0));
					Now += 4;
					break;
				}
			case "subi":
				{
					//addi a b endl
					int ind = CompFunc.GetVarIndex(Now + 1);//是否为变量名？
					int ind2 = CompFunc.GetVarIndex(Now + 2);
					CompFunc.CheckEnd(Now + 3);

					Ilist.Add(new Ins(InsID.subi, ind, ind2, 0));
					Now += 4;
					break;
				}
			case "muli":
				{
					//addi a b endl
					int ind = CompFunc.GetVarIndex(Now + 1);//是否为变量名？
					int ind2 = CompFunc.GetVarIndex(Now + 2);
					CompFunc.CheckEnd(Now + 3);

					Ilist.Add(new Ins(InsID.muli, ind, ind2, 0));
					Now += 4;
					break;
				}
			case "divi":
				{
					//addi a b 5 endl
					int ind = CompFunc.GetVarIndex(Now + 1);//是否为变量名？
					int ind2 = CompFunc.GetVarIndex(Now + 2);
					CompFunc.CheckEnd(Now + 3);

					Ilist.Add(new Ins(InsID.divi, ind, ind2, 0));
					Now += 4;
					break;
				}
			case "printi":
				{
					//printi a endl
					int i1 = CompFunc.GetVarIndex(Now + 1);//是否为合法变量名
					CompFunc.CheckEnd(Now + 2);//end of the line
					Ilist.Add(new Ins(InsID.printi, i1, 0, 0));
					Now += 3;
					break;
				}
			case "inputi":
				{
					//input a endl
					int i1 = CompFunc.GetVarIndex(Now + 1);//是否为合法变量名
					CompFunc.CheckEnd(Now + 2);//end of the line
					Ilist.Add(new Ins(InsID.inputi, i1, 0, 0));
					Now += 3;
					break;
				}
			default:
				{
					throw new Exception("未知的指令");
				}
		}
	}*/
//30行。在使TypeOfToken返回var引用后废弃
/*
 
/// <summary>
	/// 如果为临时变量，改其状态
	/// </summary>
	private void SetVarState(string nam)
	{
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].Name == nam)
			{
				if (Vtype[i].State == RegStat.Occupied)
				{
					Vtype[i].State = RegStat.Available;
				}
				return;
			}
		}
		throw new Exception("没有找到变量");
	}
	/// <summary>
	/// 返回变量符号的偏移值，如果没有会报错
	/// </summary>
	private int VarOffest(string nam)
	{
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].Name == nam)
			{
				return Vtype[i].Offest;
			}
		}
		throw new Exception("没有找到变量名");
	}

 */
//250+行。在写出De_exp函数后废弃
/*

	/// <summary>
	/// 处理布尔运算
	/// </summary>
	private void De_bool(List<int> pri, List<Token> tok, int loc, CompID compid)
	{
		Ret_TokenType typ1 = TypeofToken(tok[loc - 1].Str);
		Ret_TokenType typ2 = TypeofToken(tok[loc + 1].Str);
		if (typ1.Type != typ2.Type) { throw new Exception("等式两边类型不匹配"); }
		if (typ1.Var == null && typ2.Var == null)
		{
			bool bb;
			if (typ1.Type == VarType.Bool)
			{
				bool b1 = ToConst(tok[loc - 1].Str, VarType.Bool) == 1;
				bool b2 = ToConst(tok[loc + 1].Str, VarType.Bool) == 1;
				switch (compid)
				{
					case CompID.and: { bb = b1 && b2; break; }
					case CompID.or: { bb = b1 || b2; break; }
					case CompID.equ: { bb = b1 == b2; break; }
					case CompID.nqu: { bb = b1 != b2; break; }
					default: { throw new Exception("编译器内部错误"); }
				}
			}	 //true && true => true
			else
			{
				float b1 = Convert.ToSingle(tok[loc - 1].Str);
				float b2 = Convert.ToSingle(tok[loc + 1].Str);
				switch (compid)
				{
					case CompID.gtr: { bb = b1 > b2; break; }
					case CompID.smr: { bb = b1 < b2; break; }
					case CompID.egtr: { bb = b1 >= b2; break; }
					case CompID.esmr: { bb = b1 <= b2; break; }
					case CompID.equ: { bb = b1 == b2; break; }
					case CompID.nqu: { bb = b1 != b2; break; }
					default: { throw new Exception("编译器内部错误"); }
				}
			}								 //123 == 159 => false
			tok[loc - 1] = new Token(TokenType.Letter, BtoS(bb));
			tok.RemoveRange(loc, 2);
			pri.RemoveRange(loc, 2);
			return;
		}//优化常量表达式

		if (typ2.Var == null)
		{
			//a+2 => 2+a
			(typ1, typ2) = (typ2, typ1);
			(tok[loc - 1], tok[loc + 1]) = (tok[loc + 1], tok[loc - 1]);
			if (compid == CompID.gtr) compid = CompID.smr;
			else if (compid == CompID.smr) compid = CompID.gtr;
			else if (compid == CompID.egtr) compid = CompID.esmr;
			else if (compid == CompID.esmr) compid = CompID.egtr;
		} //保证typ2不为常量。
		if (typ1.Var == null)
		{
			InsID lod = InsID.lodi + (int)(typ1.Type - 1) * Opnum;

			RegVar vv2 = CreateVar_temp(typ1.Type);
			Ilist.Add(new Ins(lod, vv2.Offest, ToConst(tok[loc - 1].Str, typ1.Type), 0));
			Log.Print(lod, vv2.Name, tok[loc - 1].Str);
			tok[loc - 1] = new Token(TokenType.Letter, vv2.Name);
		}//对常数添加lod指令

		//至此：_a&&b or a&&b

		//== !=  c=a==b 多输入，布尔输出    equ4 nqu4 / equ1 nqu1
		if (compid == CompID.equ || compid == CompID.nqu)
		{
			InsID id = InsID.equ + (compid - CompID.equ);
			if (IsTemp(tok[loc - 1].Str) && typ1.Type == VarType.Bool)
			{
				//_a=_a&&b
				SetVarState(tok[loc + 1].Str);
				Ilist.Add(new Ins(id, VarOffest(tok[loc - 1].Str), VarOffest(tok[loc - 1].Str), VarOffest(tok[loc + 1].Str)));
				Log.Print(id, tok[loc - 1].Str, tok[loc - 1].Str, tok[loc + 1].Str);
			}
			else
			{
				RegVar v = CreateVar_temp(VarType.Bool);
				SetVarState(tok[loc - 1].Str);
				SetVarState(tok[loc + 1].Str);
				Ilist.Add(new Ins(id, v.Offest, VarOffest(tok[loc - 1].Str), VarOffest(tok[loc + 1].Str)));
				Log.Print(id, v.Name, tok[loc - 1].Str, tok[loc + 1].Str);
				tok[loc - 1] = new Token(TokenType.Letter, v.Name);
			}
			pri.RemoveRange(loc, 2);
			tok.RemoveRange(loc, 2);
			return;
		}
		//>= <=  c=a>=b 数字输入，布尔输出  gtri smri / gtrf smrf
		if (compid == CompID.gtr || compid == CompID.smr || compid == CompID.egtr || compid == CompID.esmr)
		{
#warning egtr esmr
			InsID id = InsID.gtri + (compid - CompID.gtr) + (int)(typ1.Type - 1) * Opnum;
			if(typ1.Type == VarType.Bool) { throw new Exception("比较运算仅对非bool值生效"); }
			RegVar v = CreateVar_temp(VarType.Bool);
			SetVarState(tok[loc - 1].Str);
			SetVarState(tok[loc + 1].Str);
			Ilist.Add(new Ins(id, v.Offest, VarOffest(tok[loc - 1].Str), VarOffest(tok[loc + 1].Str)));
			Log.Print(id, v.Name, tok[loc - 1].Str, tok[loc + 1].Str);

			tok[loc - 1] = new Token(TokenType.Letter, v.Name);
			pri.RemoveRange(loc, 2);
			tok.RemoveRange(loc, 2);
			return;
		}
		//&& || 布尔输入输出					and or
		if (compid == CompID.and || compid == CompID.or)
		{
			InsID id = compid == CompID.and ? InsID.and : InsID.or;
			if (IsTemp(tok[loc - 1].Str))
			{
				//_a=_a&&b
				SetVarState(tok[loc + 1].Str);
				Ilist.Add(new Ins(id, VarOffest(tok[loc - 1].Str), VarOffest(tok[loc - 1].Str), VarOffest(tok[loc + 1].Str)));
				Log.Print(id, tok[loc - 1].Str, tok[loc - 1].Str, tok[loc + 1].Str);
			}//同为temp且size相同时，才可复用
			else
			{
				RegVar v = CreateVar_temp(VarType.Bool);
				SetVarState(tok[loc - 1].Str);
				SetVarState(tok[loc + 1].Str);
				Ilist.Add(new Ins(id, v.Offest, VarOffest(tok[loc - 1].Str), VarOffest(tok[loc + 1].Str)));
				Log.Print(id, v.Name, tok[loc - 1].Str, tok[loc + 1].Str);
				tok[loc - 1] = new Token(TokenType.Letter, v.Name);
			}
			pri.RemoveRange(loc, 2);
			tok.RemoveRange(loc, 2);
			return;
		}
		
	}
	//如果b，c类型不同，则应添加类型转换指令
	/// <summary>
	/// 处理四则运算
	/// </summary>
	private void De_comp(List<int> pri, List<Token> tok, int loc, CompID compid)
	{
		Ret_TokenType typ1 = TypeofToken(tok[loc - 1].Str);
		Ret_TokenType typ2 = TypeofToken(tok[loc + 1].Str);
		if (typ1.Type != typ2.Type) { throw new Exception("表达式两端类型不同"); }

		VarType type = typ1.Type;
		InsID ins_lod, ins_set, ins_comp;

		switch (type)
		{
			case VarType.Int:
				{
					ins_lod = InsID.lodi;
					ins_set = InsID.set;
					ins_comp = (InsID)((int)InsID.addi + compid);
					break;
				}
			default:
				{
					throw new Exception("类型无法进行该运算");
				}
		}//推断操作指令

		//若全常量，优化常量表达式
		if (typ1.Var == null && typ2.Var == null)
		{
			float res;
			float f1 = Convert.ToSingle(tok[loc - 1].Str);
			float f2 = Convert.ToSingle(tok[loc + 1].Str);
			switch (compid)
			{
				case CompID.add: { res = f1 + f2; break; }
				case CompID.sub: { res = f1 - f2; break; }
				case CompID.mul: { res = f1 * f2; break; }
				case CompID.div: { res = f1 / f2; break; }
				default: { throw new Exception("无法解析的运算符"); }
			}
			tok[loc - 1] = new Token(TokenType.Digit, res.ToString());
			tok.RemoveRange(loc, 2);
			pri.RemoveRange(loc, 2);
			return;
		}
		//保证typ2不为常量。用于优化输出的字节码
		if (typ2.Var == null)
		{
			if (compid != CompID.div)
			{
				//a+2 => 2+a
				(typ1, typ2) = (typ2, typ1);
				(tok[loc - 1], tok[loc + 1]) = (tok[loc + 1], tok[loc - 1]);
			}
			else//除法不满足交换律
			{
				//a+2 => a+_a
				RegVar vv = CreateVar_temp(type);
				int ii = ToConst(tok[loc + 1].Str, type);
				Ilist.Add(new Ins(ins_lod, vv.Offest, ii, 0));
				Log.Print(ins_lod, vv.Name, tok[loc + 1].Str);
				tok[loc + 1] = new Token(TokenType.Letter, vv.Name);
			}
		}
		if (typ1.Var == null)//装载常数。变量交换后调用 3+b => _a+b
		{
			RegVar vv = CreateVar_temp(type);
			int ii = ToConst(tok[loc - 1].Str, type);
			Ilist.Add(new Ins(ins_lod, vv.Offest, ii, 0));
			Log.Print(ins_lod, vv.Name, tok[loc - 1].Str);
			tok[loc - 1] = new Token(TokenType.Letter, vv.Name);
		}

		if (IsTemp(tok[loc - 1].Str))   //_a+b => _a+=b;
		{
			SetVarState(tok[loc + 1].Str);
			Ilist.Add(new Ins(ins_comp, VarOffest(tok[loc - 1].Str), VarOffest(tok[loc + 1].Str), 0));
			Log.Print(ins_comp, tok[loc - 1].Str, tok[loc + 1].Str);
		}
		else                        //a+b => _a=a; _a+=b;
		{
			RegVar vvv = CreateVar_temp(type);
			SetVarState(tok[loc - 1].Str);
			SetVarState(tok[loc + 1].Str);
			Ilist.Add(new Ins(ins_set, vvv.Offest, VarOffest(tok[loc - 1].Str), 0));
			Log.Print(ins_set, vvv.Name, tok[loc - 1].Str);
			Ilist.Add(new Ins(ins_comp, vvv.Offest, VarOffest(tok[loc + 1].Str), 0));
			Log.Print(ins_comp, vvv.Name, tok[loc + 1].Str);
			tok[loc - 1] = new Token(TokenType.Letter, vvv.Name);
		}
		//更改符号表
		tok.RemoveRange(loc, 2);
		pri.RemoveRange(loc, 2);
	}

	/// <summary>
	/// 布尔转string
	/// </summary>
	private static string BtoS(bool v)
	{
		if (v) { return "true"; }
		return "false";
	}

	/// <summary>
	/// 某符号是否为临时变量。附报错
	/// </summary>
	private bool IsTemp(string nam)
	{
		for (int i = 0; i < Vtype.Count; i++)
		{
			if (Vtype[i].Name == nam)
			{
				if (Vtype[i].State != RegStat.Locked)return true;
				return false;
			}
		}
		throw new Exception("没有找到变量");
	}
 */
/*
 public enum CompID
{
	add,sub,mul,div,rem,

	and,or,

	equ,nqu,

	gtr,smr,
	egtr,esmr,
};
 */
//200+行。在写出新的Split函数后废弃
/*
 
public struct Ret_Presplit(int num, string str)
{
	public int Number = num;
	public string Str = str;
	public override string ToString()
	{
		return Number + " " + Str;
	}
}

public struct Ret_Analyze(List<Token> tok, List<int> pri)
{
	/// <summary>
	/// 某行的符号表
	/// </summary>
	public List<Token> Tokens = tok;
	/// <summary>
	/// 某行的符号优先级
	/// </summary>
	public List<int> Pri = pri;
}
	/// <summary>
	/// 分析并生成符号与优先级表。检查小括号
	/// </summary>
	public static Ret_Analyze Analyze(string sourcecode)
	{
		//分段
		//优先级设置
		//根据括号改变优先级
		//移除括号
		
		List<Token> starr = Split(sourcecode);
		List<int> pp = Priority(starr);

		return new Ret_Analyze(starr, pp);
	}
	/// <summary>
	/// 去除注释，处理语法糖，并把源代码分为标准行。无报错
	/// </summary>
	public static Ret_Presplit[] PreSplit(string _source)
	{
		//遵从原则：同表达式不拆
		//大括号分单独行
		//去除\r\n\t
		//遇到\r加行号
		char[] source = _source.ToCharArray();
		char[] ch = new char[source.Length];

		int now = 0;
		int start = 0;
		int line = 1;
		List<Ret_Presplit> ret = [];
		for (int i = 0; i < source.Length; i++)
		{
			if (source[i] == '\r' || source[i] == '\t')
			{
				continue;
			}
			if (source[i] == '\n')
			{
				line++;
				source[i] = ' ';
			}
			ch[now] = source[i];
			if (source[i] == ';') //分start-now
			{
				string s = new(ch, start, now - start + 1);
				if (s.Length > 0)
				{
					ret.Add(new Ret_Presplit(line, s));
				}
				start = now + 1;
			}
			else if (source[i] == '{')//分单行
			{
				string s = new(ch, start, now - start);
				if (s.Length > 0)
				{
					ret.Add(new Ret_Presplit(line, s));
				}
				ret.Add(new Ret_Presplit(line, new(source, i, 1)));
				start = now + 1;
			}
			else if (source[i] == '}')//分单行
			{
				ret.Add(new Ret_Presplit(line, new(source, i, 1)));
				start = now + 1;
			}
			now++;
		}
		return ret.ToArray();
	}

	/// <summary>
	/// 根据给出的mark
	/// </summary>
	private static List<Token> Split(string __str)
	{
		char[] str = (__str+'\0').ToCharArray();
		List<Token> ret = [];
		int now = 0;

		while(now < str.Length - 1)
		{
			//先跳过空格，此时可以计算行数
			if (str[now] == '\r' || str[now] == '\t' || 
				str[now] == '\n' || str[now] == ' ' ||
				str[now] == '\0') { now++;continue; }

			//然后分token
			ret.Add(Next(str, now));
			now += ret[^1].Str.Length;
		}
		
		while (now < str.Length - 1)
		{
			if (char.IsLetter(str[now]))
			{
				//遇到非字符和数字就停止
				int i = 0;
				while (true)
				{
					i++;
					if (!char.IsLetter(str[now + i]) && !char.IsDigit(str[now + i]))
					{
						break;
					}
				}
				ret.Add(new Token(TokenType.Letter, new string(str, now, i)));
				now += i;
			}//字符
			else if (char.IsDigit(str[now]) || str[now] == '-' && char.IsDigit(str[now + 1]))
			{
				//遇到非数字且非 .
				int i = 0;
				while (true)
				{
					i++;
					if (!char.IsDigit(str[now + i]) && str[now + i] != '.')// 
					{
						break;
					}
				}
				ret.Add(new Token(TokenType.Digit, new string(str, now, i)));
				now += i;
			}//数字
			else if (IsSymbol(str[now]))//符号
			{
				int i = SbLen(str[now], str[now + 1]);
				ret.Add(new Token(TokenType.Symbol, new string(str, now, i)));
				now += i;
			}
			else
			{
				now++;
			}
		}
		
		return ret;
	}
	/// <summary>
	/// 标记运算优先级。对于非函数的括号，变优先级并移除。检查小括号
	/// </summary>
	private static List<int> Priority(List<Token> list)
	{
		int exp = OpPriority(new Token( TokenType.Symbol,"("));
		Stack<bool> stk = new();//监测小括号 true:函数 false：小括号
		List<int> ret = new(new int[list.Count]);
		//临时算法：变优先级
		for (int i = 0; i < list.Count; i++)
		{
			bool isfunc;

			ret[i] = OpPriority(list[i]);//定下初始优先级
			if (list[i].Type == TokenType.Symbol) { ret[i] += exp * stk.Count; }

			if (list[i].Str == "(")
			{
				isfunc = i > 0 && list[i - 1].Type == TokenType.Letter;
				if (!isfunc)//为小括号，此时移除它
				{
					list.RemoveAt(i);
					ret.RemoveAt(i);
					i--;
				}
				stk.Push(isfunc);
			}
			else if (list[i].Str == ")")
			{
				try { isfunc = stk.Pop(); }
				catch { throw new Exception("小括号数量不匹配"); }
				if (!isfunc)//为小括号，此时移除它
				{
					list.RemoveAt(i);
					ret.RemoveAt(i);
					i--;
				}
				else
				{
					ret[i] -= exp;
				}
			}
		}
		if (stk.Count != 0) { throw new Exception("小括号数量不匹配"); }
		return ret;
	}
*/
/*
		Ret_Presplit[] code = SynaxAnalyzer.PreSplit(sourcecode);//返回行号与字符串

		for (int i = 0; i < code.Length; i++)
		{
			try
			{
				Ret_Analyze ra = SynaxAnalyzer.Analyze(code[i].Str);
				if (ra.Tokens.Count > 0) Code.Add(new Line(code[i].Number, ra));
			}
			catch (Exception e)
			{
				throw new Exception("Line " + code[i].Number + " " + e.Message);
			}
		}
*/
//50行。在实现类型转换时移除
/*
int
lodi,				//lodi R1 200    R1=200
//float
	//lodf,
//byte
	//lodb,
	//addb, subb, 
	//mulb, divb, modb,
	//gtrb, smrb,
	//egtrb, esmrb,
	//bool
	//lodl,

	//case InsID.lodi://lodi r1 200 0
	//	reg[ins.Para1] = ins.Para2;
	//	break;
	//case InsID.lodf:
	//	reg[ins.Para1] = ins.Para2;
//	break;
case InsID.lodb:
						*(byte*)&reg[ins.Para1] = *(byte*)&ins.Para2;
						break;
					case InsID.addb:
						*(byte*)&reg[ins.Para1] = (byte)(reg[ins.Para2] + reg[ins.Para3]);
						break;
					case InsID.subb:
						*(byte*)&reg[ins.Para1] = (byte)(reg[ins.Para2] - reg[ins.Para3]);
						break;
					case InsID.mulb:
						*(byte*)&reg[ins.Para1] = (byte)(reg[ins.Para2] * reg[ins.Para3]);
						break;
					case InsID.divb:
						*(byte*)&reg[ins.Para1] = (byte)(reg[ins.Para2] / reg[ins.Para3]);
						break;
					case InsID.modb:
						*(byte*)&reg[ins.Para1] = (byte)(reg[ins.Para2] % reg[ins.Para3]);
						break;
					case InsID.gtrb:
						reg[ins.Para1] = reg[ins.Para2] > reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.smrb:
						reg[ins.Para1] = reg[ins.Para2] < reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.egtrb:
						reg[ins.Para1] = reg[ins.Para2] >= reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.esmrb:
						reg[ins.Para1] = reg[ins.Para2] <= reg[ins.Para3] ? 1 : 0;
						break;
					case InsID.lodl:
						*(bool*)&reg[ins.Para1] = *(bool*)&ins.Para2;
						break;
*/
