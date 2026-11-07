using Avalonia.Controls;

namespace Mue.Avalonia;

/// <summary>
/// Lien du crédit « Updates by Mue ». Type propre : les styles globaux de l'hôte qui visent <c>Button</c> ne s'y
/// appliquent pas, et seul le thème explicite de la fenêtre le dessine.
/// </summary>
public sealed class CreditLinkButton : Button
{
    protected override Type StyleKeyOverride => typeof(CreditLinkButton);
}
