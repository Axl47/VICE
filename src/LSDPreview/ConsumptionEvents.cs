namespace PersonalMods.LSDPreview;

// A consumption action is distinct from the inventory object, which can be reused by a stack.
internal sealed class ConsumptionEvents
{
    private IntPtr Player,Product;
    internal void Record(IntPtr player,IntPtr product){Player=player;Product=product;}
    internal bool Take(IntPtr player,out IntPtr product)
    {
        product=Player==player ? Product:IntPtr.Zero;Clear();return product!=IntPtr.Zero;
    }
    internal void Clear(){Player=Product=IntPtr.Zero;}
}
