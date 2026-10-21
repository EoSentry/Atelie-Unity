using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Mostra as camadas de um MapSession na cena (um SpriteRenderer por camada).
    /// Ordem: mar (0) · água rasa (1) · contorno (2) · terra (3). Objetos/rotas usam ordens acima de 10.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        // Ordem de desenho de cada camada (número maior = desenha por cima). Objetos e rotas usarão 10 ou mais.
        public const int OrderSea = 0, OrderShoal = 1, OrderCoast = 2, OrderLand = 3, OrderObjects = 10;

        // a sessão (dados e camadas) que esta view está mostrando
        MapSession session;
        SpriteRenderer seaR, shoalR, coastR, landR;
        Sprite seaS, shoalS, coastS, landS;

        // Liga esta view a uma sessão. Sempre que a sessão recriar as camadas (novo mapa/abrir), a view se refaz sozinha.
        public void Bind(MapSession s)
        {
            if (session != null) session.Rebuilt -= Rebuild;
            session = s;
            session.Rebuilt += Rebuild;
            Rebuild();
        }

        void OnDestroy() { if (session != null) session.Rebuilt -= Rebuild; }

        // Acha ou cria um objeto filho com SpriteRenderer (o componente que desenha uma imagem na cena).
        SpriteRenderer Make(string name, int order)
        {
            Transform t = transform.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(transform, false);
            SpriteRenderer r = go.GetComponent<SpriteRenderer>();
            if (r == null) r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            return r;
        }

        // Cria um Sprite que mostra a textura inteira da camada. Pivô (0,0) = canto inferior esquerdo; PPU = pixels por unidade.
        static Sprite MakeSprite(PixelLayer l)
        {
            return Sprite.Create(l.tex, new Rect(0, 0, l.W, l.H), Vector2.zero, MapCamera.PPU, 0, SpriteMeshType.FullRect);
        }

        // Recria os sprites das 4 camadas (chamado ao criar/abrir um mapa).
        public void Rebuild()
        {
            if (session == null) return;
            if (seaS != null) Destroy(seaS);
            if (shoalS != null) Destroy(shoalS);
            if (coastS != null) Destroy(coastS);
            if (landS != null) Destroy(landS);

            seaR = Make("Mar", OrderSea); shoalR = Make("AguaRasa", OrderShoal);
            coastR = Make("Contorno", OrderCoast); landR = Make("Terra", OrderLand);

            seaS = MakeSprite(session.sea); seaR.sprite = seaS;
            shoalS = MakeSprite(session.shoal); shoalR.sprite = shoalS;
            coastS = MakeSprite(session.coast); coastR.sprite = coastS;
            landS = MakeSprite(session.land); landR.sprite = landS;

            // a água rasa tem resolução menor: estica para cobrir o mapa inteiro
            // a água rasa tem resolução menor: estica o sprite para cobrir o mapa inteiro
            shoalR.transform.localScale = new Vector3(session.W / (float)session.shoal.W, session.H / (float)session.shoal.H, 1f);
            ApplyVisibility();
        }

        // Liga/desliga a visibilidade de cada camada (útil para o seu menu: ex. esconder a terra).
        public bool showSea = true, showShoal = true, showCoast = true, showLand = true;

        public void ApplyVisibility()
        {
            if (seaR != null) seaR.enabled = showSea;
            if (shoalR != null) shoalR.enabled = showShoal && session.doc.shoal.on;
            if (coastR != null) coastR.enabled = showCoast;
            if (landR != null) landR.enabled = showLand;
        }

        /// <summary>Envia as regiões alteradas para a GPU. Chame uma vez por quadro.</summary>
        // Envia à GPU as regiões alteradas de todas as camadas. Chamado uma vez por quadro pelo MapEditor.
        public void Flush()
        {
            if (session == null) return;
            session.sea.Flush(); session.land.Flush(); session.coast.Flush(); session.shoal.Flush();
        }
    }
}
