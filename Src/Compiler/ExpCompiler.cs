
using ToyCompiler.Data;

namespace ToyCompiler.Compiler;

/// <summary>
/// 表达式编译器
/// </summary>
public class ExpCompiler(List<RegVar> vtype, VarType ret)
{
	/// <summary>
	/// 跳转语句类型
	/// </summary>
	private enum BranchType
	{
		None,
		If,
		While,
	}
	/// <summary>
	/// 跳转语句组
	/// </summary>
	private class BranchMark(BranchType mode)
	{
		public BranchType Mode = mode;  //跳转的类型
		public int HeadStart = 0;       //语句头算式的起始位置
		public int HeadMark = -1;       //开头的语句位置
		public List<int> TailMark = []; //语句块结尾的跳转指令位置
	}
	private struct Ret_TokenType(RegVar? var, VarType typ, bool isstatic)
	{
		public RegVar? Var = var;
		public VarType Type = typ;
		public bool IsStatic = isstatic;
	}

	/*用于编译函数内的表达式*/

	public readonly List<Ins> Ilist = [];

	private readonly List<(IFunc, int)> Calls = [];//存储函数调用
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
		if (Tokens.Count == 0) return;

		VarType typ = PublicTokens.GetVarType(Tokens, 0);
		if (typ.ID != TypeID.None)
		{
			CreateVar_user(typ, Tokens[1 + typ.Pdepth * 2].Str);
			if (Tokens.Count - 2 - typ.Pdepth * 2 == 0) return;
			//语法糖：声明变量的同时初始化
			GenIns(Pri[(1 + typ.Pdepth * 2)..], Tokens[(1 + typ.Pdepth * 2)..], false);
		}//创建变量
		else if (Tokens[0].Str == "if")
		{
			CheckBranch(Tokens, next);

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

			De_IF(Pri[3..^1], Tokens[3..^1]);       //先编译if表达式
			Bmark.Peek().HeadMark = Ilist.Count - 1;//存语句块头
		}
		else if (Tokens[0].Str == "{")
		{
			Depth++;
		}
		else if (Tokens[0].Str == "}")
		{
			Depth--;
			if (Depth < 0) throw new Exception("大括号数量不匹配");

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
				if (bm.Mode == BranchType.While)//while结尾处也添加跳转指令
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
			Log.Print(InsID.pop, i.ToString(), string.Empty);
		}
		Ilist.Add(new Ins(InsID.stsp, 0, 0, 0));
		Log.Print(InsID.stsp, string.Empty, string.Empty);
	}

	//==编译函数==================================
	private static void CheckBranch(List<Token> Tokens, string next)
	{
		if (Tokens[1].Str != "(" || Tokens[^1].Str != ")") throw new Exception("分支语句后要有小括号");
		if (next != "{") throw new Exception("分支语句后必须有大括号表示的语句块");
	}
	/// <summary>
	/// 生成表达式的字节码指令
	/// </summary>
	private void GenIns(List<int> pri, List<Token> tok, bool genpara)
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
						case "+": De_exp(pri, tok, Operators.Add, pr_loc); break;
						case "-": De_exp(pri, tok, Operators.Sub, pr_loc); break;
						case "*": De_exp(pri, tok, Operators.Mul, pr_loc); break;
						case "/": De_exp(pri, tok, Operators.Div, pr_loc); break;
						case "%": De_exp(pri, tok, Operators.Mod, pr_loc); break;
						case "=": De_equ(pri, tok, pr_loc); break;
						case "==": De_exp(pri, tok, Operators.Equ, pr_loc); break;
						case "!=": De_exp(pri, tok, Operators.Nqu, pr_loc); break;
						case ">": De_exp(pri, tok, Operators.Gtr, pr_loc); break;
						case ">=": De_exp(pri, tok, Operators.Egtr, pr_loc); break;
						case "<": De_exp(pri, tok, Operators.Smr, pr_loc); break;
						case "<=": De_exp(pri, tok, Operators.Esmr, pr_loc); break;
						case "&&": De_exp(pri, tok, Operators.And, pr_loc); break;
						case "||": De_exp(pri, tok, Operators.Or, pr_loc); break;
						case "(": De_bkt(pri, tok, pr_loc); break;
						case "[": De_arr(pri, tok, pr_loc - 1); break;
						case ".": De_Dot(pri, tok, pr_loc); break;
						default: { throw new Exception("未知的运算符：" + tok[pr_loc].Str); }
					}
					break;
				}
				pr = pri[i];
				pr_loc = i;
			}
			//如未进行解析，抛出语法错误异常
			if (processed == false) throw new Exception("语法错误");
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
			if (typ.Var == null) throw new Exception("左值应是变量");
			if (tok[loc + 3].Str != "]") throw new Exception("应输入]");
			if (typ.Var.Type.Pdepth <= 0) throw new Exception("无法将[]用于" + typ.Type + "类型的表达式");
			RegVar arr = typ.Var;//数组变量
			if (typ.IsStatic) arr = LoadPvar(arr);

			int index = 0;
			Ret_TokenType typp = TypeofToken(tok[loc + 2].Str);//如索引为常量可进行优化
			if (!PublicTokens.ConvertTo(typp.Type, new(TypeID.Int))) throw new Exception("索引应为整数");
			if (typp.Var == null) index = ToInstantNum(tok[loc + 2].Str, new VarType(TypeID.Int));

			VarType objtype = new(arr.Type.ID, arr.Type.Pdepth - 1);//解引用一次，depth减1
			RegVar v2 = GenPara(pri.Slice(loc + 5, 1), tok.Slice(loc + 5, 1), objtype);//等号右侧

			int large = arr.Type.Pdepth > 1 ? 4 : PublicTokens.SizeOf(arr.Type.ID);//数组元素大小
			InsID ins;//指令
			if (large == 4) ins = InsID.setp4;
			else if (large == 1) ins = InsID.setp1;
			else if (large == 2) ins = InsID.setp2;
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
			if (typ.Var == null) throw new Exception("左值应是变量");
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
				if (id.Item2 != InsID.stop)
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
		if (id2 != TypeID.None)
		{
			if (tok[loc + 1].Str != ")") throw new Exception("构造函数必须是无参的");
			InsID id;
			if (tok[loc - 2].Str == "stackalloc") id = InsID.salloc;
			else if (tok[loc - 2].Str == "new") id = InsID.malloc;
			else throw new Exception("语法错误");

			UserTypeDef ut = PublicTokens.GetUserType(id2);
			RegVar one = CreateVar_temp(new VarType(TypeID.Int));
			RegVar rv = CreateVar_temp(ut.Type);//装结果
			if (one.State == RegStat.Occupied) one.State = RegStat.Available;

			Ilist.Add(new Ins(InsID.lod, one.Offest, 1, 0));
			Log.Print(InsID.lod, one.Name, "1");
			Ilist.Add(new Ins(id, rv.Offest, one.Offest, ut.ActualSize * 4));
			Log.Print(id, rv.Name, one.Name, (ut.ActualSize * 4).ToString());

			pri.RemoveRange(loc - 1, 3);
			tok.RemoveRange(loc - 1, 3);
			tok[loc - 2] = new Token(TokenType.Letter, rv.Name);
			return;
		}//按照构造函数解析
		loc--;
		/* 此函数由GenIns调用。所以括号内为单长token。
		 * func(a,b,c,d) 先解析参数，再寻找函数
		 */
		List<RegVar> vars = [];
		int end = loc + 2;
		while (true)
		{
			if (tok[end - 1].Str == ")") { end--; break; }
			if (tok[end].Str == ")") { break; }

			Ret_TokenType typ = TypeofToken(tok[end].Str);
			typ.Var ??= LoadConst(tok, typ.Type, end);//如常量则装载
			if (typ.IsStatic) typ.Var = LoadPvar(typ.Var);
			vars.Add(typ.Var);
			end += 2;
		}
		IFunc func = PublicTokens.GetFunction(tok[loc].Str, vars);//寻找函数，对比定义
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

		UserTypeDef ut = PublicTokens.GetUserType(typ1.Var.Type.ID);
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
		if (rv.State == RegStat.Occupied) rv.State = RegStat.Available;
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
	private void De_exp(List<int> pri, List<Token> tok, Operators op, int loc)
	{
		Ret_TokenType typ1 = TypeofToken(tok[loc - 1].Str);//左侧符号的字符串
		Ret_TokenType typ2 = TypeofToken(tok[loc + 1].Str);//右侧符号的字符串
		if (!typ1.Type.Equ(typ2.Type) &&
			!PublicTokens.ConvertTo(typ1.Type, new(TypeID.Int)) ||
			!PublicTokens.ConvertTo(typ2.Type, new(TypeID.Int)))
			throw new Exception("运算符" + op + "无法用于" + typ1.Type + "与" + typ2.Type);

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
		tok[loc - 1] = new Token(TokenType.Letter, vv.Name);
		tok.RemoveRange(loc, 2);
		pri.RemoveRange(loc, 2);

	}

	//==辅助函数：指令类型选择=====================

	private static (InsID, InsID) SwitchIns_Convert(TypeID t, VarType s)
	{
		if (s.Pdepth > 0) throw new Exception("无法转换指针类型");
		if (s.ID == t || t == TypeID.Int && s.ID == TypeID.Byte) throw new Exception("多余的类型转换");


		InsID step1 = s.ID == TypeID.Float ? InsID.f2i : InsID.stop;
		InsID step2 = InsID.stop;
		switch (t)
		{
			case TypeID.Float: step2 = InsID.i2f; break;
			case TypeID.Int: break;
			case TypeID.Byte: step2 = InsID.i2b; break;
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
		if (typ1.Var != null)
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
	public RegVar CreateVar_user(VarType typ, string name)
	{
		if (name == "int" || name == "bool") throw new Exception("不能以关键字命名变量");
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
			if (nam.Contains('.')) return new Ret_TokenType(null, new VarType(TypeID.Float), false);
			return new Ret_TokenType(null, new VarType(TypeID.Int), false);
		}//以数字常量计
		else if (nam == "true" || nam == "false")
		{
			return new Ret_TokenType(null, new VarType(TypeID.Bool), false);
		}//以布尔常量计
		else if (nam.StartsWith('"'))
		{
			return new Ret_TokenType(null, new VarType(TypeID.Char, 1), false);
		}//以字符数组常量计
		else
		{
			for (int i = 0; i < Vtype.Count; i++)
			{
				if (Vtype[i].Name != nam) continue;
				return new Ret_TokenType(Vtype[i], Vtype[i].Type, false);
			}//找私有变量
			RegVar? vv = PublicTokens.GetVar(nam);   //找公共变量
			if (vv != null) return new Ret_TokenType(vv, vv.Type, true);
			throw new Exception("未知的变量名：" + nam);
		}//以变量计
	}
	/// <summary>
	/// 把非指针的常量符号转为立即数。附带隐式类型转换
	/// </summary>
	private static unsafe int ToInstantNum(string nam, VarType typ)
	{
		if (typ.Pdepth != 0) throw new Exception("无法把常量赋值给指针");
		switch (typ.ID)
		{
			case TypeID.Byte: return Convert.ToByte(nam);
			case TypeID.Int: return Convert.ToInt32(nam);
			case TypeID.Bool:
				if (nam == "true") return 1;
				if (nam == "false") return 0;
				throw new Exception("常量 " + nam + "不是布尔类型");
			case TypeID.Float:
				float ff = Convert.ToSingle(nam);
				return *(int*)&ff;
			case TypeID.Char: return Convert.ToInt16(nam);
			default: throw new Exception("无法把常量" + nam + "解析为" + typ);
		}
	}
}
