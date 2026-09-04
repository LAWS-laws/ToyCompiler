

using System.Text;

namespace ToyCompiler.Data;

/// <summary>
/// 一个标准行
/// </summary>
public struct Line
{
	public int Number;
	public List<Token> Tok;
	public List<int> Pri;

	public Line()
	{
		Number = 0;
		Tok = [];
		Pri = [];
	}
	public Line(int num, List<Token> tok)
	{
		Number = num;
		Tok = tok;
		Pri = [];
	}
	public Line(int num, List<Token> tok, List<int> pri)
	{
		Number = num; Tok = tok; Pri = pri;
	}

	public override readonly string ToString()
	{
		StringBuilder sb = new();
		sb.Append(Number);
		for (int i = 0; i < Tok.Count; i++)
		{
			sb.Append('~');
			sb.Append(Tok[i].Str);
		}
		return sb.ToString();
	}
}