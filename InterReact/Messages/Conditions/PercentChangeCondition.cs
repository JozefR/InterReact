using System.Globalization;

namespace InterReact.Messages.Conditions;

public class PercentChangeCondition : ContractCondition
{

    protected override string Value
    {
        get
        {
            return ChangePercent.ToString(NumberFormatInfo.InvariantInfo);
        }
        set
        {
            ChangePercent = double.Parse(value, NumberFormatInfo.InvariantInfo);
        }
    }

    public double ChangePercent { get; set; }
}
