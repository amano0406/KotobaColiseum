using System.Text;

namespace KotobaColiseum.Web.Services;

public sealed class PlaceholderArtService
{
    public string CreateEnemyPortrait(string enemyName)
    {
        var svg = $$"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 900" shape-rendering="crispEdges">
          <rect x="260" y="180" width="380" height="80" fill="#ffc74e" opacity="0.95" />
          <rect x="220" y="260" width="460" height="360" rx="28" fill="#ffdca4" />
          <rect x="300" y="330" width="90" height="90" fill="#1a120f" />
          <rect x="510" y="330" width="90" height="90" fill="#1a120f" />
          <rect x="340" y="360" width="28" height="28" fill="#fff6e2" />
          <rect x="550" y="360" width="28" height="28" fill="#fff6e2" />
          <rect x="388" y="438" width="120" height="34" fill="#c24b25" />
          <rect x="300" y="610" width="300" height="130" fill="#b63d18" />
          <rect x="248" y="684" width="108" height="56" fill="#8d2c12" />
          <rect x="544" y="684" width="108" height="56" fill="#8d2c12" />
        </svg>
        """;

        return ToDataUri(svg);
    }

    public string CreateBattlefield()
    {
        var svg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1600 900" shape-rendering="crispEdges">
          <defs>
            <linearGradient id="sky" x1="0" x2="1" y1="0" y2="1">
              <stop offset="0%" stop-color="#120b08" />
              <stop offset="55%" stop-color="#5b1d10" />
              <stop offset="100%" stop-color="#ff8d2f" />
            </linearGradient>
            <linearGradient id="ground" x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" stop-color="#40120a" />
              <stop offset="100%" stop-color="#140704" />
            </linearGradient>
          </defs>
          <rect width="1600" height="900" fill="url(#sky)" />
          <rect x="1180" y="110" width="180" height="180" fill="#ffd361" opacity="0.88" />
          <rect x="0" y="560" width="1600" height="340" fill="url(#ground)" />
          <rect x="0" y="680" width="1600" height="20" fill="#2d120c" opacity="0.5" />
          <rect x="180" y="470" width="200" height="170" fill="#3c2017" />
          <rect x="228" y="420" width="104" height="58" fill="#ffb347" />
          <rect x="470" y="500" width="230" height="150" fill="#432117" />
          <rect x="528" y="450" width="114" height="52" fill="#ff9c43" />
          <rect x="820" y="448" width="260" height="182" fill="#4d2418" />
          <rect x="874" y="394" width="152" height="58" fill="#ffb347" />
          <rect x="1180" y="492" width="220" height="158" fill="#432117" />
          <rect x="1238" y="442" width="104" height="48" fill="#ff9c43" />
          <rect x="190" y="640" width="1210" height="126" fill="#26100c" opacity="0.72" />
        </svg>
        """;

        return ToDataUri(svg);
    }

    private static string ToDataUri(string svg)
    {
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }
}
