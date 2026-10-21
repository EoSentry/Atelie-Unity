using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Atelie
{
    /// <summary>Ferramentas disponíveis nesta etapa.</summary>
    public enum Tool { Pan, Terrain, Sea, EraseLand }

    /// <summary>Configurações do pincel (a UI altera estes valores).</summary>
    [System.Serializable]
    public class BrushSettings
    {
        public float size = 90f;       // diâmetro em pixels do mapa
        public float hardness = .8f;   // 0 = borda toda macia … 1 = borda dura
        public float alpha = 1f;       // opacidade da tinta
        public float roughness = .35f; // ruído na borda (0 = redondo, 1 = muito quebrado)
        public float roughScale = 1f;  // tamanho do detalhe do ruído
        public float blend = .4f;      // suavização com o que já está pintado
        public bool landOnly = false;  // terreno: só recolore onde já existe terra
        public bool sink = true;       // mar: também apaga a terra por baixo
    }

    /// <summary>
    /// Cérebro do editor. Recebe o mouse/teclado, aplica o pincel, controla o histórico e agenda o
    /// cálculo da água rasa. Toda a interface (menus, botões) deve apenas chamar os métodos
    /// públicos desta classe.
    /// </summary>
    public class MapEditor : MonoBehaviour
    {
        [Header("Assets (opcional: sem biblioteca, usa texturas geradas)")]
        public AtelieLibrary library;

        [Header("Ferramenta e pincel")]
        public Tool tool = Tool.Terrain;
        public BrushSettings brush = new BrushSettings();
        public string terrainId;       // id do terreno escolhido
        public string seaId;           // id do mar escolhido

        [Header("Referências (preenchidas sozinhas se vazias)")]
        public MapCamera mapCamera;
        public MapView view;

        public MapSession session;     // o mapa aberto
        public System.Func<Vector2, bool> PointerBlocked;   // a UI define: "o mouse está sobre a UI?"

        public event System.Action ToolChanged;               // ferramenta/terreno mudou (UI atualiza)
        public event System.Action<string> Message;           // avisos de texto para a UI mostrar

        // cache: um TexSampler por terreno (para não reler a textura a cada carimbo)
        readonly Dictionary<string, TexSampler> samplers = new Dictionary<string, TexSampler>();

        // ---- estado do traço em andamento ----
        bool stroking;                 // true enquanto o botão do mouse está apertado pintando
        Vector2 lastPt;                // último ponto carimbado
        float strokeAcc;               // distância "sobrando" entre carimbos
        RectInt strokeBox;             // área alterada pelo traço
        RectInt pendingLand;           // área de terra alterada neste quadro (contorno é refeito uma vez só)
        Vector2 pointerMap;            // posição do mouse em coordenadas do mapa
        public Vector2 PointerMap { get { return pointerMap; } }
        public bool PointerInside { get; private set; }   // o mouse está dentro do mapa?

        // ---- água rasa em segundo plano ----
        Task<ShoalBuilder.Result> shoalTask;   // cálculo em andamento (outra thread)
        float shoalWait;                       // tempo esperando o traço terminar

        public bool Busy { get; private set; } // true durante salvar/abrir (bloqueia pintura)

        // =====================================================================
        void Awake()
        {
            // procura a câmera e a view se não foram arrastadas no Inspector
            if (mapCamera == null) mapCamera = FindObjectOfType<MapCamera>();
            if (view == null) view = GetComponentInChildren<MapView>();
        }

        /// <summary>Cria o primeiro mapa. Chamado pelo AtelieBootstrap (ou pela sua própria cena).</summary>
        public void Init(AtelieLibrary lib, int w, int h)
        {
            library = lib;
            List<TerrainDef> land = library.Land(), sea = library.Sea();
            // se nada foi escolhido ainda, usa o primeiro terreno/mar da lista
            if (string.IsNullOrEmpty(terrainId) && land.Count > 0) terrainId = land[0].Id;
            if (string.IsNullOrEmpty(seaId) && sea.Count > 0) seaId = sea[0].Id;
            MapDocument d = new MapDocument { width = w, height = h, seaTexture = seaId };
            d.mapId = System.Guid.NewGuid().ToString("N");      // identificador único do mapa
            session = new MapSession(d, SamplerFor(seaId));
            AttachSession();
        }

        /// <summary>Liga a sessão (dados) à view (desenho) e enquadra a câmera.</summary>
        void AttachSession()
        {
            if (view == null)
            {
                GameObject g = new GameObject("MapView");
                g.transform.SetParent(transform, false);
                view = g.AddComponent<MapView>();
            }
            view.Bind(session);
            session.RebuildCoast();
            if (mapCamera != null) mapCamera.Fit(session.W, session.H);
            if (ToolChanged != null) ToolChanged();
        }

        // ---------------- assets ----------------

        /// <summary>Devolve (e guarda em cache) o "amostrador" de textura de um terreno.</summary>
        public TexSampler SamplerFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            TexSampler s;
            if (samplers.TryGetValue(id, out s)) return s;
            TerrainDef t = library != null ? library.FindTerrain(id) : null;
            if (t == null || t.texture == null) return null;
            s = new TexSampler(t.texture, t.tileSize, t.tint);
            samplers[id] = s;
            return s;
        }

        /// <summary>Esqueça o cache (use se trocar a textura de um terreno com o jogo rodando).</summary>
        public void ClearSamplerCache() { samplers.Clear(); }

        // métodos para a sua UI chamar:
        public void SetTool(Tool t) { tool = t; if (ToolChanged != null) ToolChanged(); }
        public void SetTerrain(string id) { terrainId = id; tool = Tool.Terrain; if (ToolChanged != null) ToolChanged(); }
        public void SetSea(string id) { seaId = id; tool = Tool.Sea; if (ToolChanged != null) ToolChanged(); }

        void Say(string m) { if (Message != null) Message(m); }

        // ---------------- mapa ----------------

        /// <summary>Descarta o mapa atual e cria um novo em branco.</summary>
        public void NewMap(int w, int h, string name)
        {
            MapDocument d = new MapDocument { width = w, height = h, name = name, seaTexture = seaId };
            d.mapId = System.Guid.NewGuid().ToString("N");
            session.Build(d, SamplerFor(seaId));
            AttachSession();
            Say("Novo mapa " + session.W + "×" + session.H);
        }

        /// <summary>Salva na pasta Mapas/&lt;nome&gt;.</summary>
        public void SaveProject()
        {
            Busy = true;
            try { string f = ProjectIO.FolderFor(session.doc.name); ProjectIO.Save(session, f); Say("Salvo em: " + f); }
            catch (System.Exception e) { Say("Falha ao salvar: " + e.Message); Debug.LogException(e); }
            Busy = false;
        }

        /// <summary>Abre um projeto salvo (caminho da pasta).</summary>
        public bool LoadProject(string folder)
        {
            Busy = true;
            bool ok = false;
            try { ok = ProjectIO.Load(session, folder); if (ok) { AttachSession(); Say("Aberto: " + session.doc.name); } else Say("Projeto não encontrado."); }
            catch (System.Exception e) { Say("Falha ao abrir: " + e.Message); Debug.LogException(e); }
            Busy = false;
            return ok;
        }

        /// <summary>Exporta a imagem do mapa em PNG (pasta Exportados).</summary>
        public void ExportPng()
        {
            try
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "Exportados");
                System.IO.Directory.CreateDirectory(dir);
                string f = System.IO.Path.Combine(dir, ProjectIO.SafeName(session.doc.name) + ".png");
                ProjectIO.ExportPng(session, f);
                Say("Imagem exportada: " + f);
            }
            catch (System.Exception e) { Say("Falha ao exportar: " + e.Message); Debug.LogException(e); }
        }

        /// <summary>Enche o mar inteiro com a textura de mar atual (pode ser desfeito).</summary>
        public void FillSea()
        {
            BeginStroke();
            TexSampler ts = SamplerFor(seaId);
            if (ts != null) { session.sea.Touch(0, 0, session.sea.W, session.sea.H); session.FillSea(ts); }
            EndStroke();
        }

        public void Undo() { session.Undo(); view.ApplyVisibility(); }
        public void Redo() { session.Redo(); view.ApplyVisibility(); }

        // =====================================================================
        // pincel

        /// <summary>Começa um traço: liga a gravação do desfazer nas duas camadas editáveis.</summary>
        void BeginStroke()
        {
            session.sea.BeginRecord();
            session.land.BeginRecord();
            stroking = true;
            strokeAcc = 0f;
            strokeBox = new RectInt(0, 0, 0, 0);
        }

        /// <summary>Termina o traço e guarda um passo de desfazer (se algo mudou).</summary>
        void EndStroke()
        {
            if (!stroking) return;
            stroking = false;
            RasterPatch ps = session.sea.EndRecord();
            RasterPatch pl = session.land.EndRecord();
            if (ps != null || pl != null)
            {
                RasterUndo u = new RasterUndo();
                if (ps != null) u.patches.Add(ps);
                if (pl != null) u.patches.Add(pl);
                session.Push(u);
            }
        }

        /// <summary>Aplica UM carimbo do pincel no ponto (x, y) conforme a ferramenta atual.</summary>
        RectInt BrushAt(float x, float y)
        {
            BrushSettings b = brush;
            float r = b.size * .5f;                       // raio
            RectInt box = new RectInt(0, 0, 0, 0);
            if (tool == Tool.Terrain)
            {
                // pinta terra com a textura escolhida (Atop = só recolore terra que já existe)
                TexSampler t = SamplerFor(terrainId);
                box = BrushEngine.StampBlend(session.land, x, y, r, t, b.hardness, b.alpha,
                        b.landOnly ? Comp.Atop : Comp.Over, b.roughness, b.roughScale, b.blend, false);
                if (!b.landOnly) pendingLand = BrushEngine.Union(pendingLand, box);
            }
            else if (tool == Tool.Sea)
            {
                // pinta o mar; e se "sink" estiver ligado, abre um buraco na terra (rios, lagos, golfos)
                TexSampler t = SamplerFor(seaId);
                BrushEngine.StampBlend(session.sea, x, y, r, t, b.hardness, b.alpha, Comp.Over, b.roughness, b.roughScale, b.blend, true);
                if (b.sink)
                {
                    box = BrushEngine.Stamp(session.land, x, y, r, null, b.hardness, 1f, Comp.DestOut, b.roughness, b.roughScale);
                    pendingLand = BrushEngine.Union(pendingLand, box);
                }
            }
            else if (tool == Tool.EraseLand)
            {
                // borracha: apaga a terra
                box = BrushEngine.Stamp(session.land, x, y, r, null, b.hardness, 1f, Comp.DestOut, b.roughness, b.roughScale);
                pendingLand = BrushEngine.Union(pendingLand, box);
            }
            return box;
        }

        /// <summary>
        /// Arrastar do último ponto até o novo: coloca carimbos a cada ~20% do raio para o traço
        /// ficar contínuo, lembrando a sobra (strokeAcc) para o espaçamento ser uniforme entre quadros.
        /// </summary>
        void BrushLine(Vector2 p)
        {
            float r = brush.size * .5f;
            float step = Mathf.Max(2f, r * .2f);           // distância entre carimbos
            Vector2 d = p - lastPt;
            float dist = d.magnitude;
            float pos = step - strokeAcc;
            while (pos <= dist)
            {
                float t = dist > 0f ? pos / dist : 0f;
                RectInt b = BrushAt(lastPt.x + d.x * t, lastPt.y + d.y * t);
                strokeBox = BrushEngine.Union(strokeBox, b);
                pos += step;
            }
            strokeAcc = dist - (pos - step);
            lastPt = p;
        }

        bool IsPaintTool { get { return tool == Tool.Terrain || tool == Tool.Sea || tool == Tool.EraseLand; } }

        // =====================================================================
        /// <summary>Roda todo quadro: lê o mouse, pinta, atualiza contorno, água rasa e envia à GPU.</summary>
        void Update()
        {
            if (session == null || mapCamera == null) return;

            Vector2 mp = Inp.MousePos;
            bool blocked = PointerBlocked != null && PointerBlocked(mp);       // mouse sobre a UI?
            pointerMap = mapCamera.ScreenToMap(mp);                             // posição no mapa
            PointerInside = pointerMap.x >= 0 && pointerMap.y >= 0 && pointerMap.x < session.W && pointerMap.y < session.H;

            HandleKeys();

            // só pinta se: é ferramenta de pintura, não está movendo a câmera e não está salvando
            bool canPaint = IsPaintTool && !mapCamera.IsPanning && !Inp.Held(Hk.Space) && !Busy;

            if (canPaint && !stroking && Inp.LeftDown && !blocked && PointerInside)
            {
                // clique: começa um traço e carimba uma vez
                BeginStroke();
                lastPt = pointerMap;
                RectInt b = BrushAt(pointerMap.x, pointerMap.y);
                strokeBox = BrushEngine.Union(strokeBox, b);
            }
            else if (stroking)
            {
                // botão segurado: continua o traço; solto (ou cancelado): fecha o traço
                if (Inp.LeftHeld && canPaint) BrushLine(pointerMap);
                if (Inp.LeftUp || !Inp.LeftHeld || !canPaint) EndStroke();
            }

            // o contorno da costa é refeito UMA vez por quadro, na área toda que mudou
            if (pendingLand.width > 0 && pendingLand.height > 0) session.OnLandChanged(pendingLand);
            pendingLand = new RectInt(0, 0, 0, 0);

            UpdateShoal();
            view.Flush();     // envia as regiões alteradas para a GPU
        }

        /// <summary>Atalhos de teclado.</summary>
        void HandleKeys()
        {
            if (stroking) return;
            bool ctrl = Inp.Held(Hk.Ctrl), shift = Inp.Held(Hk.Shift);
            if (ctrl && Inp.Pressed(Hk.Z)) { if (shift) Redo(); else Undo(); }   // Ctrl+Z / Ctrl+Shift+Z
            else if (ctrl && Inp.Pressed(Hk.Y)) Redo();                          // Ctrl+Y
            else if (ctrl && Inp.Pressed(Hk.S)) SaveProject();                   // Ctrl+S
            if (!ctrl)
            {
                if (Inp.Pressed(Hk.LBracket)) brush.size = Mathf.Clamp(Mathf.Round(brush.size * .88f), 10f, 600f);   // [ diminui
                if (Inp.Pressed(Hk.RBracket)) brush.size = Mathf.Clamp(Mathf.Round(brush.size * 1.14f), 10f, 600f);  // ] aumenta
                if (Inp.Pressed(Hk.F)) mapCamera.Fit(session.W, session.H);                                          // F enquadra
            }
        }

        // ---------------- água rasa (cálculo em outra thread) ----------------

        /// <summary>
        /// Se a terra mudou, recalcula a água rasa em segundo plano (para não travar o pincel),
        /// esperando o traço terminar. Quando o cálculo acaba, copia o resultado para a camada.
        /// </summary>
        void UpdateShoal()
        {
            if (shoalTask != null)
            {
                // há um cálculo rodando: verifica se terminou
                if (shoalTask.IsCompleted)
                {
                    ShoalBuilder.Result r = shoalTask.IsFaulted ? null : shoalTask.Result;
                    shoalTask = null;
                    // só aceita se o tamanho ainda bate (o mapa pode ter sido trocado enquanto calculava)
                    if (r != null && r.w == session.shoal.W && r.h == session.shoal.H)
                    {
                        System.Array.Copy(r.px, session.shoal.px, r.px.Length);
                        session.shoal.MarkAllDirty();
                    }
                }
                return;
            }
            if (!session.shoalDirty) return;                       // nada a recalcular
            if (stroking) { shoalWait = 0f; return; }              // espera o traço terminar
            shoalWait += Time.unscaledDeltaTime;
            if (shoalWait < .25f) return;                          // espera 0,25 s de calma
            shoalWait = 0f;
            session.shoalDirty = false;
            view.ApplyVisibility();

            int w = session.shoal.W, h = session.shoal.H;
            // copia as configurações (a outra thread não deve ler objetos que a UI está mudando)
            ShoalSettings s = JsonUtility.FromJson<ShoalSettings>(JsonUtility.ToJson(session.doc.shoal));
            byte[] mask = ShoalBuilder.SampleMask(session.land, w, h);   // máscara de terra pequena
            float k = session.ShoalScale;
            shoalTask = Task.Run(() => ShoalBuilder.Build(mask, w, h, k, s));   // roda em segundo plano
        }

        /// <summary>Peça para recalcular a água rasa (ex.: depois de mudar largura/intensidade).</summary>
        public void RefreshShoal() { session.shoalDirty = true; view.ApplyVisibility(); }
    }
}
