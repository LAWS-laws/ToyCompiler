
namespace ToyCompiler.Data;

/// <summary>
/// 寄存器状态
/// </summary>
public enum RegStat
{
	UnUsed,     //未使用
	Locked,     //用户变量，不可复用
	Occupied,   //可复用但被占
	Available,  //可立即复用
};
