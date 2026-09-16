using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// Length counted in Unicode code points, not UTF-16 units (VR-002, spec I-5), which
/// <c>StringLength</c> cannot express. A null value is left to <c>Required</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CodePointLengthAttribute(int minimum, int maximum) : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string text)
        {
            return true;
        }

        var length = text.EnumerateRunes().Count();
        return length >= minimum && length <= maximum;
    }
}
