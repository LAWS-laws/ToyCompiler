
namespace ToyCompiler.Data;

/// <summary>
/// 符号
/// </summary>
public struct Token(TokenType typ, string str)
{
	public TokenType Type = typ;
	public string Str = str;
	public override string ToString()
	{
		return "[" + Type.ToString()[0] + "]   " + Str;
	}
}
