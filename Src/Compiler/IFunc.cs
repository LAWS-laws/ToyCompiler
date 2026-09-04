using ToyCompiler.Data;

namespace ToyCompiler.Compiler;

/// <summary>
/// 库函数和函数继承此接口
/// </summary>
public interface IFunc
{
	string Name { get; }        //函数名
	List<VarType> Para { get; } //函数参数
	VarType RetType { get; }    //返回值类型
	int Head { get; }       //函数头部指令位置
}
