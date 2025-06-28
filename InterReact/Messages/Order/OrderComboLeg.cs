namespace InterReact.Messages.Order;

public sealed class OrderComboLeg(double price) // input + output
{
    public double Price { get; } = price;
}
