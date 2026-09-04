
using ToyCompiler.Data;
using ToyCompiler.Compiler;

namespace ToyCompiler.Preprocess;

/// <summary>
/// 输入源码。转换为Token[]和优先级列表
/// </summary>
public class SynaxAnalyzer
{
	/// <summary>
	/// for语句的解析结果
	/// </summary>
	private struct Ret_DecodeFOR(Line l1, Line l2, Line l3)
	{
		public Line Line1 = l1;
		public Line Line2 = l2;
		public Line Line3 = l3;
	}

	//此类下并无成员数据。它是一系列语法分析函数的集合。

	//运算符集合
	private static readonly (string, int)[] OP2 =
	[
		(">>",8),("<<",8),("||",2),("&&",3),("==",6),("!=",6),(">=",7),("<=",7),
		("+=",1),("-=",1),("*=",1),("/=",1),("%=",1),("&=",1),("|=",1),
	];
	private static readonly (string, int)[] OP1 =
	[
		("=",1),(">",7), ("<",7), ("+",9), ("-",9), ("*",10),("/",10),("%",10),("&",5),
		("|",4),("!",11),("(",12),(")",12),("[",12),("]",12),("{",0), ("}",0), (",",0),
		(";",0),(".",12),
	];
	/* 重复next，遇到{};就分一整行 */
	/// <summary>
	/// 把源码拆分为标准行，每行有小括号检查
	/// </summary>
	public static List<Line> Split2(string sourcecode)
	{
		char[] str = (sourcecode + '\0').ToCharArray();
		List<Line> ret = [new Line(0, [])];
		Stack<int> brackets = new();//0：小括号 1：函数 2：for循环 3：类型转换
		int exp = OpPriority(new Token(TokenType.Symbol, "("));//优先等级数
		int line = 1;
		int now = 0;

		while (now < str.Length - 1)
		{
			char c = str[now];
			int skipmode = DetectSkip(c);
			if (skipmode >= 1)
			{
				now++;
				line += skipmode - 1;
				continue;
			}//跳过空格并计算行数

			if (c == ';' && (brackets.Count == 0 || brackets.Peek() != 2) || c == '{' || c == '}')
			{
				if (c != ';')//括号独立成行
					ret.Add(new Line(line, [new Token(TokenType.Symbol, c.ToString())]));
				ret.Add(new Line());
				now++;
				if (brackets.Count > 0) throw new Exception("小括号数量不匹配");
				brackets.Clear();
				continue;
			}//创建标准行，清除栈
			if (ret[^1].Tok.Count == 0) ret[^1] = new Line(line, ret[^1].Tok);//设置行号

			Token tok = Next(str, now);
			if (tok.Type != TokenType.Notes)
			{
				int priority = OpPriority(tok);//定下初始优先级
				if (priority != -1) priority += exp * brackets.Count;
				if (tok.Str == "(" || tok.Str == "[")
				{
					if (ret[^1].Tok[^1].Type != TokenType.Letter && tok.Str == "(") brackets.Push(0);//小括号
					else if (ret[^1].Tok[^1].Str == "for") brackets.Push(2);          //for循环
					else brackets.Push(1);                                          //函数
					if (brackets.Peek() == 0)
					{
						if (tok.Str == "[") throw new Exception("语法错误");
						now += tok.Str.Length; continue;
					}   //小括号
				}
				else if (tok.Str == ")" || tok.Str == "]")
				{
					if (brackets.Pop() == 0)
					{/*插入对类型转换的判定*/
						if (PublicTokens.GetTypeID(ret[^1].Tok[^1].Str) != TypeID.None)
						{
							priority--;
							ret[^1].Tok.Insert(ret[^1].Tok.Count - 1, new Token(TokenType.Symbol, "("));
							ret[^1].Pri.Insert(ret[^1].Pri.Count - 1, priority - exp);
						}//是类型转换
						else
						{
							now += tok.Str.Length;
							continue;
						}//小括号
					}
					priority -= exp;
				}
				ret[^1].Pri.Add(priority);
				ret[^1].Tok.Add(tok);   //添加当前符号
			}
			now += tok.Str.Length;  //改下一个符号的头
		}
		for (int i = 0; i < ret.Count; i++)
		{
			if (ret[i].Tok.Count == 0) { ret.RemoveAt(i); i--; }
		}//移除空行
		DecodeSynaxCandy(ret);
		return ret;
	}

	/// <summary>
	/// 解析语法糖并检查大括号
	/// </summary>
	private static void DecodeSynaxCandy(List<Line> code)
	{
		Stack<bool> brackets = new();
		Stack<Line> lines = new();  //存for循环自增语句
		for (int i = 0; i < code.Count; i++)
		{
			for (int j = 0; j < code[i].Tok.Count; j++)
			{/*char转换*/
				if (!code[i].Tok[j].Str.StartsWith('\'')) continue;
				if (code[i].Tok[j].Str.Length != 3) throw new Exception("不支持的char格式");

				code[i].Tok[j] = new Token(TokenType.Digit, ((int)code[i].Tok[j].Str[1]).ToString());
			}
			//for循环转换
			if (code[i].Tok[0].Str == "{")
			{
				brackets.Push(i > 0 && code[i - 1].Tok[0].Str == "for");
				if (!brackets.Peek()) continue;//i-1行是for循环

				Ret_DecodeFOR r = DecodeFOR(code[i - 1]);
				if (r.Line1.Tok.Count > 0)
				{
					code.Insert(i - 1, r.Line1); i++;
				}
				code[i - 1] = r.Line2;
				lines.Push(r.Line3);
			}
			else if (code[i].Tok[0].Str == "}")
			{
				if (!brackets.Pop()) continue;//是for循环结束括号

				Line l = lines.Pop();
				if (l.Tok.Count > 0)
				{
					code.Insert(i, l); i++;
				}
			}
		}
		if (lines.Count > 0) throw new Exception("大括号数量不匹配");
	}
	private static Ret_DecodeFOR DecodeFOR(Line line)
	{
		int d1 = -1;
		int d2 = -1;
		if (line.Tok.Count == 1 || line.Tok[1].Str != "(" || line.Tok[^1].Str != ")") throw new Exception("没写小括号");
		for (int i = 0; i < line.Tok.Count; i++)
		{
			if (line.Tok[i].Str != ";") continue;
			if (d1 == -1) d1 = i;
			else if (d2 == -1) d2 = i;
			else throw new Exception("语法错误：多写了分号");
		}//找到分隔的分号
		if (d1 == -1 || d2 == -1) throw new Exception("语法错误：少写了分号");

		Line line2 = new(line.Number, line.Tok[(d1 + 1)..d2], line.Pri[(d1 + 1)..d2]);

		line2.Tok.Insert(0, new Token(TokenType.Symbol, "("));
		line2.Tok.Insert(0, new Token(TokenType.Letter, "while"));
		line2.Tok.Add(new Token(TokenType.Symbol, ")"));

		line2.Pri.Insert(0, OpPriority(line2.Tok[1]));
		line2.Pri.Insert(0, 0);
		line2.Pri.Add(OpPriority(line2.Tok[1]));

		if (line2.Tok.Count == 3)//while()
		{
			line2.Tok.Insert(2, new Token(TokenType.Letter, "true"));
			line2.Pri.Insert(2, 0);
		}

		return new Ret_DecodeFOR(new Line(line.Number, line.Tok[2..d1], line.Pri[2..d1]), line2,
								 new Line(line.Number, line.Tok[(d2 + 1)..^1], line.Pri[(d2 + 1)..^1]));
	}
	//辅助函数
	/// <summary>
	/// 0:其他 1:跳过 2:跳过且加行号
	/// </summary>
	private static int DetectSkip(char c)
	{
		if (c == '\t' || c == '\r' || c == '\0' || c == ' ') return 1;
		if (c == '\n') return 2;
		return 0;
	}
	/// <summary>
	/// 从now开始截取出一个符号
	/// </summary>
	private static Token Next(char[] str, int now)
	{
		int i = 1;//用于保存符号长度
		TokenType typ;
		if (str[now] == '/' && str[now + 1] == '/')
		{
			while (str[now + i] != '\n' && str[now + i] != '\0') i++;
			typ = TokenType.Notes;
		}//单行注释
		else if (str[now] == '/' && str[now + 1] == '*')
		{
			i++;
			while (str[now + i] != '*' || str[now + i + 1] != '/') i++;
			i += 2;
			typ = TokenType.Notes;
		}/*多行注释*/
		else if (IsLetter(str[now]))
		{
			while (IsLetter(str[now + i]) || char.IsDigit(str[now + i])) i++;//字符或数字可接
			typ = TokenType.Letter;
		}//单词
		else if (char.IsDigit(str[now]) || str[now] == '-' && char.IsDigit(str[now + 1]))
		{
			while (char.IsDigit(str[now + i]) || str[now + i] == '.') i++;//数字和小数点可接
			typ = TokenType.Digit;
		}//数字
		else if (OpMes(str[now], 'a').Item1 != -1)
		{
			i = SbLen(str[now], str[now + 1]);
			typ = TokenType.Symbol;
		}//符号
		else if (str[now] == '\'' || str[now] == '"')
		{
			while (str[now + i] != str[now]) i++;
			i++;
			typ = TokenType.Letter;
		}//字符
		else
		{
			throw new Exception("未知的符号：" + str[now]);
		}//报错

		return new Token(typ, new string(str, now, i));
	}
	/// <summary>
	/// 返回长度和优先级。未找到返回-1
	/// </summary>
	private static (int, int) OpMes(char c1, char c2)
	{
		for (int i = 0; i < OP2.Length; i++)
		{
			if (OP2[i].Item1[0] == c1 && OP2[i].Item1[1] == c2) return (2, OP2[i].Item2);
		}
		for (int i = 0; i < OP1.Length; i++)
		{
			if (OP1[i].Item1[0] == c1) return (1, OP1[i].Item2);
		}
		return (-1, -1);
	}
	/// <summary>
	/// 操作符的长度
	/// </summary>
	private static int SbLen(char c1, char c2)
	{
		var vv = OpMes(c1, c2);
		if (vv.Item1 == -1) throw new Exception("未知符号：" + c1 + c2);
		return vv.Item1;
	}
	/// <summary>
	/// 运算的优先级，不是运算符会返回-1
	/// </summary>
	private static int OpPriority(Token tok)
	{
		if (tok.Type != TokenType.Symbol) return -1;
		return OpMes(tok.Str[0], tok.Str.Length == 1 ? 'a' : tok.Str[1]).Item2;
	}
	/// <summary>
	/// 是否为中文或英文字符
	/// </summary>
	private static bool IsLetter(char c)
	{
		return char.IsLetter(c) || c > 255;
	}
}
