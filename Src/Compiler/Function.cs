using ToyCompiler.Data;

namespace ToyCompiler.Compiler;

/// <summary>
/// 表示函数
/// </summary>
public class Function : IFunc
{
	public VarType RetType { get; }     //返回类型
	public List<VarType> Para { get; }  //参数类型
	public string Name { get; }         //函数名
	public int Head { get; set; }       //函数头部位置。用于链接
	public int Ilength => Excomp.Ilist.Count;//指令长度

	public readonly List<RegVar> RegVars;   //寄存器表。用于编译

	private readonly ExpCompiler Excomp;        //表达式编译器。每个函数配一个
	private readonly int Start;
	private readonly int End;
	public Function(List<Token> tok, int start, int end)
	{
		/* void Func ( int a , int b , int c ) */
		/* Person[][] Func ( int [] a, int [] b ) */
		int now = 0;
		Start = start; End = end;
		RegVars = [];
		Para = [];
		for (int i = 0; i < Config.MaxPara; i++)
		{
			RegVars.Add(new RegVar(string.Empty, new VarType(TypeID.None), RegStat.UnUsed, i));
		}//初始化寄存器表
		RetType = PublicTokens.GetVarType(tok, 0);//解析返回值类型
		now += 1 + RetType.Pdepth * 2;
		Excomp = new ExpCompiler(RegVars, RetType);

		if (RetType.Equ(TypeID.None) && tok[0].Str != "void") throw new Exception("没有找到类型：" + tok[0].Str);
		Name = tok[now].Str;
		if (tok[now + 1].Str != "(" || tok[^1].Str != ")") throw new Exception("函数定义错误：小括号");
		//解析参数表
		if (tok.Count - now == 3) return;//无参函数
		for (int i = now + 2; i < tok.Count;)
		{
			VarType vt = PublicTokens.GetVarType(tok, i);
			if (vt.ID == TypeID.None) throw new Exception("未知的参数类型：" + tok[i].Str);
			i += 1 + vt.Pdepth * 2;
			Excomp.CreateVar_user(vt, tok[i].Str);
			Para.Add(vt);
			if (i != tok.Count - 2 && tok[i + 1].Str != ",") throw new Exception("函数参数要以逗号隔开");
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
