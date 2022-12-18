using FarNet;

namespace MultiHasher
{
	[ModuleTool(Name = nameof(MultiHasher), Options = ModuleToolOptions.F11Menus, Id = "230914eb-7a72-4820-9d78-5f3039a83220")]
	public class MultiHasherTool : ModuleTool
	{
		public override void Invoke(object sender, ModuleToolEventArgs e)
		{
			MultiHasherHost.HashMenu.Show();
		}
	}
}
