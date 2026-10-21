using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Painel de TESTE feito com IMGUI (OnGUI). Serve para você experimentar o motor sem montar UI.
    /// Quando fizer o seu menu de verdade, apague este componente e chame os métodos públicos do MapEditor.
    /// </summary>
    public class AtelieHud : MonoBehaviour
    {
        public MapEditor editor;
        public MapCamera mapCamera;

        // área do painel (a UI bloqueia o pincel sob ela)
        Rect panel = new Rect(8, 8, 270, 600);
        Vector2 scroll;
        string msg = "";
        float msgT;
        string newW = "3072", newH = "2048", newName = "Novo mapa";
        bool showOpen;
        string[] projects = new string[0];

        // Liga o painel ao editor: avisos de texto aparecem no painel e o mouse sobre o painel não pinta nem move a câmera.
        void Start()
        {
            if (editor != null)
            {
                editor.PointerBlocked = IsOverPanel;
                editor.Message += m => { msg = m; msgT = Time.unscaledTime; };
            }
            if (mapCamera != null) mapCamera.PointerBlocked = IsOverPanel;
        }

        // O mouse está sobre o painel? (IMGUI conta o Y de cima para baixo, por isso 'Screen.height - y')
        bool IsOverPanel(Vector2 screenPos)
        {
            Vector2 g = new Vector2(screenPos.x, Screen.height - screenPos.y);
            return panel.Contains(g);
        }

        // Uma linha com nome + controle deslizante + valor.
        static float Slider(string label, float v, float min, float max, string fmt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(92));
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.Label(v.ToString(fmt), GUILayout.Width(40));
            GUILayout.EndHorizontal();
            return v;
        }

        // Desenha o painel inteiro. OnGUI roda várias vezes por quadro (modo imediato do Unity).
        void OnGUI()
        {
            if (editor == null || editor.session == null) return;
            panel.height = Screen.height - 16;
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 6, panel.y + 6, panel.width - 12, panel.height - 12));
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("ATELIÊ DE MAPAS  (painel de teste)");
            GUILayout.Label(editor.session.doc.name + "  " + editor.session.W + "×" + editor.session.H);

            // ferramentas
            GUILayout.Label("Ferramenta");
            GUILayout.BeginHorizontal();
            if (Tog("Terra", editor.tool == Tool.Terrain)) editor.SetTool(Tool.Terrain);
            if (Tog("Mar", editor.tool == Tool.Sea)) editor.SetTool(Tool.Sea);
            if (Tog("Apagar", editor.tool == Tool.EraseLand)) editor.SetTool(Tool.EraseLand);
            if (Tog("Mover", editor.tool == Tool.Pan)) editor.SetTool(Tool.Pan);
            GUILayout.EndHorizontal();

            // terrenos
            if (editor.library != null)
            {
                GUILayout.Label("Terrenos");
                List<TerrainDef> land = editor.library.Land();
                GUILayout.BeginHorizontal();
                int col = 0;
                for (int i = 0; i < land.Count; i++)
                {
                    bool sel = editor.terrainId == land[i].Id && editor.tool == Tool.Terrain;
                    if (Tog(land[i].Label, sel)) editor.SetTerrain(land[i].Id);
                    if (++col == 2 && i < land.Count - 1) { col = 0; GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                }
                GUILayout.EndHorizontal();

                GUILayout.Label("Mares");
                List<TerrainDef> sea = editor.library.Sea();
                GUILayout.BeginHorizontal();
                col = 0;
                for (int i = 0; i < sea.Count; i++)
                {
                    bool sel = editor.seaId == sea[i].Id;
                    if (Tog(sea[i].Label, sel && editor.tool == Tool.Sea)) editor.SetSea(sea[i].Id);
                    if (++col == 2 && i < sea.Count - 1) { col = 0; GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                }
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Preencher tudo com o mar atual")) editor.FillSea();
            }

            // pincel
            GUILayout.Space(6);
            GUILayout.Label("Pincel  ( [ e ] mudam o tamanho )");
            BrushSettings b = editor.brush;
            b.size = Slider("Tamanho", b.size, 10, 600, "0");
            b.hardness = Slider("Dureza", b.hardness, 0, 1, "0.00");
            b.alpha = Slider("Opacidade", b.alpha, .05f, 1, "0.00");
            b.roughness = Slider("Borda quebrada", b.roughness, 0, 1, "0.00");
            b.roughScale = Slider("Escala do ruído", b.roughScale, .4f, 3, "0.0");
            b.blend = Slider("Suavizar mescla", b.blend, 0, 1, "0.00");
            b.landOnly = GUILayout.Toggle(b.landOnly, "Terreno só recolore onde já tem terra");
            b.sink = GUILayout.Toggle(b.sink, "Mar também apaga a terra");

            // água rasa
            GUILayout.Space(6);
            GUILayout.Label("Água rasa na costa");
            ShoalSettings sh = editor.session.doc.shoal;
            bool ch = false;
            bool on = GUILayout.Toggle(sh.on, "Automática"); if (on != sh.on) { sh.on = on; ch = true; }
            float w = Slider("Largura", sh.width, 10, 220, "0"); if (!Mathf.Approximately(w, sh.width)) { sh.width = w; ch = true; }
            float it = Slider("Intensidade", sh.intensity, 0, 1, "0.00"); if (!Mathf.Approximately(it, sh.intensity)) { sh.intensity = it; ch = true; }
            bool ln = GUILayout.Toggle(sh.lines, "Linhas de ondulação"); if (ln != sh.lines) { sh.lines = ln; ch = true; }
            GUILayout.BeginHorizontal();
            for (int i = 0; i < ShoalBuilder.ToneNames.Length; i++)
            {
                if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (Tog(ShoalBuilder.ToneNames[i], sh.tone == i)) { sh.tone = i; ch = true; }
            }
            GUILayout.EndHorizontal();
            if (ch) editor.RefreshShoal();

            // histórico
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.enabled = editor.session.CanUndo;
            if (GUILayout.Button("Desfazer (Ctrl+Z)")) editor.Undo();
            GUI.enabled = editor.session.CanRedo;
            if (GUILayout.Button("Refazer (Ctrl+Y)")) editor.Redo();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Enquadrar o mapa (F)")) mapCamera.Fit(editor.session.W, editor.session.H);

            // arquivo
            GUILayout.Space(6);
            GUILayout.Label("Arquivo");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Nome", GUILayout.Width(50));
            editor.session.doc.name = GUILayout.TextField(editor.session.doc.name);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Salvar (Ctrl+S)")) editor.SaveProject();
            if (GUILayout.Button("Abrir…")) { projects = ProjectIO.ListProjects(); showOpen = !showOpen; }
            if (GUILayout.Button("Exportar PNG")) editor.ExportPng();
            GUILayout.EndHorizontal();
            if (showOpen)
            {
                if (projects.Length == 0) GUILayout.Label("(nenhum mapa salvo ainda)");
                for (int i = 0; i < projects.Length; i++)
                    if (GUILayout.Button(System.IO.Path.GetFileName(projects[i]))) { editor.LoadProject(projects[i]); showOpen = false; }
            }

            GUILayout.Space(6);
            GUILayout.Label("Novo mapa");
            GUILayout.BeginHorizontal();
            newW = GUILayout.TextField(newW, GUILayout.Width(60)); GUILayout.Label("×", GUILayout.Width(12));
            newH = GUILayout.TextField(newH, GUILayout.Width(60));
            if (GUILayout.Button("Criar"))
            {
                int nw, nh;
                if (int.TryParse(newW, out nw) && int.TryParse(newH, out nh)) editor.NewMap(nw, nh, newName);
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(msg) && Time.unscaledTime - msgT < 8f)
            {
                GUILayout.Space(6);
                GUILayout.Label(msg);
            }
            GUILayout.Space(6);
            GUILayout.Label("Botão do meio/direito (ou Espaço+clique): mover\nRoda: zoom");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // Botão que fica 'apertado' quando selecionado. Devolve true só no quadro em que foi clicado.
        static bool Tog(string label, bool on)
        {
            bool r = GUILayout.Toggle(on, label, "Button");
            return r && !on;
        }
    }
}
