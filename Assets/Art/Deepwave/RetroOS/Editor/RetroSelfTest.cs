using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Apps;
using Deepwave.RetroOS.Apps.Games;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Icons;
using Deepwave.RetroOS.Web.Html;

namespace Deepwave.RetroOS.EditorTools
{
    // A headless check of the parts easy to break and hard to see: the HTML/CSS engine,
    // the folder mounts, the address resolver, the icon painter. Deepwave > RetroOS > Run Self Test, or
    // -executeMethod Deepwave.RetroOS.EditorTools.RetroSelfTest.RunFromCommandLine in CI.
    public static class RetroSelfTest
    {
        static int s_failures;
        static readonly List<string> s_log = new();

        /// <summary>Runs every check and writes the result to the console. Reached from the RetroOS Setup window, or from a command line.</summary>
        public static void Run()
        {
            s_failures = 0;
            s_log.Clear();

            TestIcons();
            TestPalette();
            TestHtml();
            TestCss();
            TestStorage();
            TestShares();
            TestNetwork();
            TestSecurity();
            TestMedia();
            TestLyrics();
            TestCards();

            var summary = string.Join("\n", s_log);
            if (s_failures == 0)
            {
                // Two checks pass by making the OS warn (a looping rule, a duplicate share name), so a passing test must leave the console clean.
                var passed = s_log.Count(l => l.StartsWith("  ok"));
                Debug.Log($"RetroOS self test: all {passed} checks passed. Any RetroOS warnings " +
                          "above are guards firing under test, which is what those checks are.\n" +
                          summary);
            }
            else Debug.LogError($"RetroOS self test: {s_failures} check(s) failed.\n" + summary);
        }

        public static void RunFromCommandLine()
        {
            Run();
            if (s_failures > 0) EditorApplication.Exit(1);
        }

        static void Check(string what, bool condition, string detail = null)
        {
            if (condition) { s_log.Add("  ok   " + what); return; }
            s_failures++;
            s_log.Add("  FAIL " + what + (detail == null ? "" : "  -> " + detail));
        }

        // --- icons ---------------------------------------------------------

        static void TestIcons()
        {
            s_log.Add("Icons");
            // Exact, not a floor: the count is quoted in the store listing and
            // the README, so losing one has to fail here rather than pass quietly.
            Check("the icon set is 71 strong", RetroIcons.All.Length == 71,
                RetroIcons.All.Length + " found");

            // Every icon at every resolution and style - a blank one is invisible on a button until a customer finds it.
            var blank = new List<string>();
            var threw = new List<string>();
            var flooded = new List<string>();

            foreach (var icon in RetroIcons.All)
                foreach (var size in new[] { 32, 48, 64 })
                    foreach (var vivid in new[] { true, false })
                    {
                        var label = $"{icon}@{size}{(vivid ? "" : " plain")}";
                        try
                        {
                            var canvas = new IconCanvas(size);
                            RetroIconLibrary.Paint(icon, canvas, vivid);

                            var opaque = 0;
                            var edge = 0;
                            for (var y = 0; y < size; y++)
                                for (var x = 0; x < size; x++)
                                {
                                    if (canvas.GetPixel(x, y).a <= 0) continue;
                                    opaque++;
                                    if (x == 0 || y == 0 || x == size - 1 || y == size - 1) edge++;
                                }

                            if (opaque < 40) blank.Add(label);

                            // A full outer ring means the painter drew past
                            // its cell.
                            if (edge >= (size - 1) * 4) flooded.Add(label);
                        }
                        catch (Exception e) { threw.Add(label + ": " + e.GetType().Name); }
                    }

            Check("every icon paints without throwing", threw.Count == 0,
                string.Join(", ", threw));
            Check("every icon paints something visible", blank.Count == 0,
                string.Join(", ", blank));
            Check("no icon floods its whole cell", flooded.Count == 0,
                string.Join(", ", flooded));

            TestIconResize();
        }

        // AtSize is on every icon's path: it must recognise a sprite this class painted and repaint it at another size.
        static void TestIconResize()
        {
            var lost = new List<string>();
            var wrongSize = new List<string>();

            foreach (var icon in RetroIcons.All)
            {
                var small = RetroIcons.Get(icon, 32);
                var big = RetroIcons.AtSize(small, 48);

                if (big == small) { lost.Add(icon.ToString()); continue; }
                if (!big || !big.texture || big.texture.width != 48)
                    wrongSize.Add(icon + " -> " + (big && big.texture ? big.texture.width : -1));

                // And back again, to the sprite the cache already holds.
                if (RetroIcons.AtSize(big, 32) != small) lost.Add(icon + " (back to 32)");
            }

            Check("every icon can be asked for at another size", lost.Count == 0,
                string.Join(", ", lost));
            Check("and comes back at the size asked for", wrongSize.Count == 0,
                string.Join(", ", wrongSize));

            Check("an icon already at the right size is handed straight back",
                RetroIcons.AtSize(RetroIcons.Get(BuiltinIcon.Folder, 48), 48) ==
                RetroIcons.Get(BuiltinIcon.Folder, 48));

            // A project's own artwork is not ours to repaint, even one named like ours (name-matching once replaced a customer's texture).
            var foreign = Sprite.Create(new Texture2D(16, 16), new Rect(0, 0, 16, 16),
                new Vector2(0.5f, 0.5f));
            foreign.name = "RetroIcon.Folder";
            Check("a sprite we did not paint is left alone",
                RetroIcons.AtSize(foreign, 48) == foreign);

            foreign.name = "RetroIcon.Nonsense";
            Check("whatever it is called", RetroIcons.AtSize(foreign, 48) == foreign);

            var texture = foreign.texture;
            UnityEngine.Object.DestroyImmediate(foreign);
            UnityEngine.Object.DestroyImmediate(texture);

            Check("and nothing at all is not a crash", RetroIcons.AtSize(null, 48) == null);
        }

        // --- palette ---------------------------------------------------------

        // The palette contract: every colour the kit paints with has a slot, and every slot reads back as the colour it names. A slotless theme colour stops following the scheme.
        static void TestPalette()
        {
            s_log.Add("Palette");

            var missing = new List<string>();
            foreach (PaletteSlot slot in Enum.GetValues(typeof(PaletteSlot)))
            {
                if (slot == PaletteSlot.None) continue;
                if (RetroSkin.Read(slot) == Color.clear) missing.Add(slot.ToString());
            }
            Check("every slot reads a colour", missing.Count == 0, string.Join(", ", missing));

            var pairs = new (PaletteSlot Slot, Color Colour)[]
            {
                (PaletteSlot.Face, RetroSkin.Face),
                (PaletteSlot.FaceDark, RetroSkin.FaceDark),
                (PaletteSlot.FaceLight, RetroSkin.FaceLight),
                (PaletteSlot.Text, RetroSkin.Text),
                (PaletteSlot.TextDisabled, RetroSkin.TextDisabled),
                (PaletteSlot.TitleActive, RetroSkin.Title),
                (PaletteSlot.TitleActiveEnd, RetroSkin.TitleEnd),
                (PaletteSlot.TitleIdle, RetroSkin.TitleIdle),
                (PaletteSlot.TitleIdleEnd, RetroSkin.TitleIdleEnd),
                (PaletteSlot.TitleText, RetroSkin.TitleText),
                (PaletteSlot.Field, RetroSkin.Field),
                (PaletteSlot.Selection, RetroSkin.Selection),
                (PaletteSlot.SelectionText, RetroSkin.SelectionText),
                (PaletteSlot.Link, RetroSkin.Link),
                (PaletteSlot.LinkVisited, RetroSkin.LinkVisited),
                (PaletteSlot.Alert, RetroSkin.Alert),
                (PaletteSlot.Desktop, RetroSkin.Desktop),
                (PaletteSlot.Screen, RetroSkin.Screen),
                (PaletteSlot.ScreenText, RetroSkin.ScreenText),
                (PaletteSlot.ScreenDim, RetroSkin.ScreenDim)
            };

            var wrong = new List<string>();
            foreach (var (slot, colour) in pairs)
                if (RetroSkin.Read(slot) != colour) wrong.Add(slot.ToString());
            Check("and reads back the colour it names", wrong.Count == 0, string.Join(", ", wrong));

            // The properties above are one per slot, so anything the enum
            // gained without being wired up shows here as a count.
            Check("and the two lists are the same length",
                pairs.Length == Enum.GetValues(typeof(PaletteSlot)).Length - 2,
                pairs.Length + " checked of " + (Enum.GetValues(typeof(PaletteSlot)).Length - 1) +
                " (FaceShadow is the theme's alone)");

            // A scheme owns twelve slots; the rest are the theme's and must survive a scheme change untouched.
            // Field joined them so High Contrast Black does not write white on the theme's white paper.
            var scheme = RetroColorScheme.All[0];
            var claimed = 0;
            foreach (PaletteSlot slot in Enum.GetValues(typeof(PaletteSlot)))
                if (slot != PaletteSlot.None && scheme.TryRead(slot, out _)) claimed++;
            Check("a scheme repaints twelve slots", claimed == 12, claimed + " claimed");

            // The reason Field is now one of them, stated as a check: no
            // scheme may write its text in the same colour as its paper.
            var unreadable = RetroColorScheme.All
                .Where(s => ColourDistance(s.Text, s.Field) < 0.25f)
                .Select(s => s.Name)
                .ToList();
            Check("every scheme's text is readable on its own field",
                unreadable.Count == 0, string.Join(", ", unreadable));

            // What a repaint would do, with no screen: a slot read back through another scheme must give that scheme's value, even if some other slot shares the colour.
            var black = RetroColorScheme.All[13];
            Check("High Contrast Black is the one that collides",
                black.Name == "High Contrast Black", black.Name);
            black.TryRead(PaletteSlot.Text, out var body);
            black.TryRead(PaletteSlot.TitleText, out var caption);
            Check("its body text and its captions are the same colour", body == caption);

            scheme.TryRead(PaletteSlot.TitleText, out var standardCaption);
            scheme.TryRead(PaletteSlot.Text, out var standardBody);
            Check("and Windows Standard tells them apart", standardBody != standardCaption);
        }

        // --- html ----------------------------------------------------------

        // Perceived distance between two palette colours: a scheme is unreadable when text and paper are the same brightness.
        static float ColourDistance(Color a, Color b) =>
            Mathf.Abs(a.grayscale - b.grayscale);

        static void TestHtml()
        {
            s_log.Add("HTML parser");

            const string html = @"<html><head><title>Test Page</title></head>
<body class=""main"">
  <h1>Heading</h1>
  <p>Body text with <b>bold</b> and a <a href=""next.html"">link</a>.</p>
  <ul><li>one<li>two</ul>
  <img src=""img/logo.png"" width=""32"">
  <!-- a comment -->
  <table><tr><td>cell</td></tr></table>
</body></html>";

            var doc = HtmlParser.Parse(html);
            Check("title is read", doc.Find("title")?.InnerText == "Test Page",
                doc.Find("title")?.InnerText);
            Check("body carries its class", doc.Find("body")?.Attr("class") == "main");
            Check("heading survives", doc.Find("h1")?.InnerText == "Heading");
            Check("link href is read", doc.Find("a")?.Attr("href") == "next.html");
            Check("image src is read", doc.Find("img")?.Attr("src") == "img/logo.png");
            Check("unclosed <li> auto-closes", doc.FindAll("li").Count() == 2,
                doc.FindAll("li").Count() + " found");
            Check("comment is dropped", !doc.InnerText.Contains("a comment"));
            Check("table cell survives", doc.Find("td")?.InnerText == "cell");

            var entities = HtmlParser.Parse("<p>A &amp; B &lt;tag&gt; &copy; &#65;</p>");
            Check("entities decode", entities.Find("p")?.InnerText == "A & B <tag> © A",
                entities.Find("p")?.InnerText);

            TestHtmlNesting();
            TestHtmlMalformed();
        }

        // 90s pages nest tables; an implicit close must stay inside its own table/list, or the inner one loses its rows to the outer.
        static void TestHtmlNesting()
        {
            var nested = HtmlParser.Parse(
                "<table><tr><td>outer<table><tr><td>inner</td></tr></table></td></tr></table>");

            var tables = nested.FindAll("table").ToList();
            var rows = nested.FindAll("tr").ToList();
            Check("a nested table survives parsing", tables.Count == 2, tables.Count + " tables");
            Check("both rows survive", rows.Count == 2, rows.Count + " rows");

            if (tables.Count == 2 && rows.Count == 2)
            {
                Check("each row belongs to its own table",
                    OwnerTable(rows[0]) != OwnerTable(rows[1]),
                    "both rows resolved to the same table");
            }

            var deep = HtmlParser.Parse(
                "<table><tr><td><table><tr><td><table><tr><td>x</td></tr>" +
                "</table></td></tr></table></td></tr></table>");
            Check("three tables deep still parses",
                deep.FindAll("table").Count() == 3 && deep.FindAll("tr").Count() == 3);

            var lists = HtmlParser.Parse("<ul><li>a<ul><li>b</li><li>c</li></ul></li></ul>")
                .FindAll("ul").ToList();
            Check("a nested list survives parsing", lists.Count == 2, lists.Count + " lists");
            if (lists.Count == 2)
                Check("the inner list keeps its items",
                    lists[1].Children.Count(c => string.Equals(c.Tag, "li",
                        StringComparison.OrdinalIgnoreCase)) == 2);

            // The implicit closes that were already relied on.
            Check("unclosed <li> still auto-closes",
                HtmlParser.Parse("<ul><li>one<li>two</ul>").FindAll("li").Count() == 2);
            Check("unclosed <p> still auto-closes",
                HtmlParser.Parse("<p>one<p>two").FindAll("p").Count() == 2);
            Check("unclosed <td> still auto-closes",
                HtmlParser.Parse("<table><tr><td>a<td>b</tr></table>").FindAll("td").Count() == 2);
        }

        static HtmlNode OwnerTable(HtmlNode node)
        {
            for (var p = node.Parent; p != null; p = p.Parent)
                if (string.Equals(p.Tag, "table", StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        // Pages are hand-written; a truncated close tag once looped the parser forever, so these run with a stopwatch.
        static void TestHtmlMalformed()
        {
            var cases = new (string name, string html)[]
            {
                ("truncated </style", "<html><style>body{color:red}</style"),
                ("truncated </script", "<p>hi</p><script>var a=1;</script"),
                ("truncated </textarea", "<textarea>x</textarea"),
                ("unclosed tags", "<div><p>text"),
                ("stray brackets", "a < b > c"),
                ("empty document", ""),
                ("deep nesting", string.Concat(Enumerable.Repeat("<div>", 2000)))
            };

            foreach (var (name, html) in cases)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    HtmlParser.Parse(html);
                    watch.Stop();
                    Check("survives " + name, watch.ElapsedMilliseconds < 2000,
                        watch.ElapsedMilliseconds + "ms");
                }
                catch (Exception e)
                {
                    Check("survives " + name, false, e.GetType().Name + ": " + e.Message);
                }
            }

            // char.ConvertFromUtf32 throws on these, and a page is allowed to
            // contain nonsense.
            foreach (var bad in new[] { "a&#xD800;b", "a&#x110000;b", "a&#99999999;b", "a&#0;b" })
            {
                try { Check("decodes " + bad, HtmlParser.Decode(bad) != null); }
                catch (Exception e) { Check("decodes " + bad, false, e.GetType().Name); }
            }

            // Nesting is capped: the recursive renderer builds a layout group per level, so a runaway document costs more to draw than parse.
            var runaway = HtmlParser.Parse(
                string.Concat(Enumerable.Repeat("<div>", 5000)) + "still here");
            var depth = 0;
            var node = runaway;
            while (node.Children.Count > 0) { node = node.Children[0]; depth++; }
            Check("runaway nesting is capped", depth < 300, depth + " levels deep");
            Check("capped content is not dropped", runaway.InnerText.Contains("still here"));
        }

        // --- css -----------------------------------------------------------

        static void TestCss()
        {
            s_log.Add("CSS");

            var sheet = new CssStylesheet();
            sheet.Parse(@"
/* comment */
body { background: #c0c0c0; color: black; padding: 12px; }
h1 { color: #000080; font-size: 26px; }
.notice { background: #ffffcc; border: 1px #808080; }
#main a { color: rgb(0,0,238); }
");
            Check("rules parsed", sheet.RuleCount >= 4, sheet.RuleCount + " rules");

            var body = new HtmlNode("body");
            var bodyStyle = sheet.StyleFor(body);
            Check("body background", bodyStyle.Background.HasValue &&
                Mathf.Approximately(bodyStyle.Background.Value.r, 192f / 255f));
            Check("shorthand padding", bodyStyle.PaddingLeft == 12f, bodyStyle.PaddingLeft?.ToString());

            var heading = new HtmlNode("h1");
            Check("font-size in px", sheet.StyleFor(heading).FontSize == 26f);

            var notice = new HtmlNode("div");
            notice.Attributes["class"] = "notice";
            Check("class selector matches", sheet.StyleFor(notice).Background.HasValue);

            TestCssMalformed();
            Check("border shorthand gives a width", sheet.StyleFor(notice).BorderWidth == 1f);

            // Descendant selector: "#main a" must not match a bare <a>.
            var loose = new HtmlNode("a");
            Check("descendant selector is scoped", !sheet.StyleFor(loose).Color.HasValue);

            var wrapper = new HtmlNode("div");
            wrapper.Attributes["id"] = "main";
            var scoped = wrapper.Add(new HtmlNode("a"));
            Check("descendant selector matches inside", sheet.StyleFor(scoped).Color.HasValue);

            Check("named colour", CssStylesheet.ParseColor("teal").HasValue);
            Check("em length uses the basis", CssStylesheet.ParseLength("2em", 10f) == 20f);
        }

        // --- storage -------------------------------------------------------

        // The two rules the OS must never get wrong: a link cannot leave its served folder, and one user cannot read another's files.
        // Both fail quietly, so they are checked explicitly. Deliberately-missing files are allowed when the page says so.
        static void TestSiteLinks(NetworkService net, string site)
        {
            var folder = RetroStorage.SiteFolder(site);
            if (!Directory.Exists(folder))
            {
                Check("the sample site exists", false, folder);
                return;
            }

            var broken = new List<string>();
            var checked_ = 0;

            foreach (var file in Directory.GetFiles(folder, "*.htm*"))
            {
                var page = Path.GetFileName(file);
                string body;
                try { body = File.ReadAllText(file); }
                catch (Exception e) { broken.Add(page + ": " + e.GetType().Name); continue; }

                foreach (Match match in Regex.Matches(body, "(?:href|src)=\"([^\"#?]+)\""))
                {
                    var reference = match.Groups[1].Value;
                    if (reference.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                        reference.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // The page names this one as missing on purpose.
                    if (reference.EndsWith("floorplan.png", StringComparison.OrdinalIgnoreCase)) continue;

                    checked_++;
                    if (!net.Request(reference, site, "").Ok) broken.Add(page + " -> " + reference);
                }
            }

            Check($"every link on {site} resolves ({checked_} checked)", broken.Count == 0,
                string.Join("; ", broken));
        }

        // A rule naming a path the file system lacks is silently dead. The demo shipped three - written before drives became nodes, missing the "/C:".
        static void TestRulePaths()
        {
            // Rules live on the scene's machines now, so this checks what the designer actually has open.
            var machines = UnityEngine.Object.FindObjectsByType<RetroOsBootstrap>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (machines.Length == 0)
            {
                s_log.Add("  --   rule paths: no machine in the open scene");
                return;
            }

            var machine = RetroStorage.MachineFolder(RetroSampleContent.Workstation);
            if (!Directory.Exists(machine)) { Check("the sample machine exists", false, machine); return; }

            var bay = new DriveBay();
            bay.Mount(DiskMount.MountDrives(machine));
            var fs = new VirtualFileSystem(bay.Root);

            var dead = new List<string>();
            var looked = 0;

            foreach (var rule in machines.SelectMany(m => m.Rules))
            {
                if (rule == null) continue;
                // Trigger arguments that look like paths.
                var argument = rule.argument?.Trim();
                if (!string.IsNullOrEmpty(argument) && argument.StartsWith("/") && !argument.EndsWith("*"))
                {
                    looked++;
                    if (fs.Resolve(argument) == null) dead.Add($"{rule.eventId} <- {argument}");
                }

                // Actions that have to resolve a node to do anything.
                foreach (var action in rule.actions)
                {
                    if (action.kind is not (OsActionKind.RevealNode or OsActionKind.HideNode
                        or OsActionKind.UnlockNode)) continue;
                    var value = action.value?.Trim();
                    if (string.IsNullOrEmpty(value)) continue;

                    looked++;
                    if (fs.Resolve(value) == null) dead.Add($"{action.kind} -> {value}");
                }
            }

            Check($"every rule path in the scene resolves ({looked} checked)",
                dead.Count == 0, string.Join("; ", dead));
        }

        static void TestSecurity()
        {
            s_log.Add("Security");

            Check("a site folder contains itself", RetroStorage.IsInside("C:/site", "C:/site"));
            Check("a page inside is allowed", RetroStorage.IsInside("C:/site", "C:/site/page.html"));
            Check("a nested asset is allowed", RetroStorage.IsInside("C:/site", "C:/site/img/logo.png"));
            Check("walking to the parent is refused",
                !RetroStorage.IsInside("C:/site", "C:/site/../secret.txt"));
            Check("walking out mid-path is refused",
                !RetroStorage.IsInside("C:/site", "C:/site/a/../../out.txt"));
            Check("a sibling with the same prefix is refused",
                !RetroStorage.IsInside("C:/site", "C:/site-evil/x.txt"));
            Check("an unrelated absolute path is refused",
                !RetroStorage.IsInside("C:/site", "C:/Windows/System32"));

            // A machine with two accounts, laid out the way DiskMount infers it.
            var drive = new VfsDirectory("C:") { Role = NodeRole.Drive };
            var users = drive.Add(new VfsDirectory("Users"));
            users.Role = NodeRole.UserRoot;

            var alice = users.Add(new VfsDirectory("alice"));
            alice.Role = NodeRole.UserHome;
            alice.Owner = "alice";
            var diary = alice.Add(new VfsFile("diary.txt", FileKind.Text));
            diary.Owner = "alice";

            var bob = users.Add(new VfsDirectory("bob"));
            bob.Role = NodeRole.UserHome;
            bob.Owner = "bob";

            var shared = drive.Add(new VfsDirectory("Shared"));
            var readme = shared.Add(new VfsFile("readme.txt", FileKind.Text));

            Check("a user sees their own file", diary.IsVisibleTo("alice"));
            Check("another user does not", !diary.IsVisibleTo("bob"));
            Check("another user does not see the home either", !alice.IsVisibleTo("bob"));
            Check("owner match ignores case", diary.IsVisibleTo("ALICE"));
            Check("unowned files are visible to everyone", readme.IsVisibleTo("bob"));
            Check("no signed-in user means no restriction", diary.IsVisibleTo(null));

            var byBob = users.List(false, "bob").Select(n => n.Name).ToList();
            Check("a listing hides the other user's home", !byBob.Contains("alice"),
                string.Join(", ", byBob));
            var byAlice = users.List(false, "alice").Select(n => n.Name).ToList();
            Check("a listing keeps your own home", byAlice.Contains("alice"),
                string.Join(", ", byAlice));

            // Rules that trigger each other must not take the process with them.
            var bus = new OsEventBus();
            var depth = 0;
            bus.Subscribe("selftest.loop", _ => { depth++; bus.Raise("selftest.loop"); });
            try
            {
                bus.Raise("selftest.loop");
                Check("a self-raising event is capped", depth <= 20, depth + " nested raises");
            }
            catch (Exception e)
            {
                Check("a self-raising event is capped", false, e.GetType().Name);
            }
        }

        // Stylesheets are hand-written too: whatever is in the file, the parser finishes and the page renders.
        static void TestCssMalformed()
        {
            var broken = new[]
            {
                "", "   ", "p {", "p { color", "p { color: }", "p { : red }",
                "{ color: red }", "}", "p { color: red", "/* unterminated",
                "@media screen { p { color: red } }", "@import url(x.css)",
                "p{color:red}}}}", "p , , q { color: red }", "#  { color: red }",
                "p{color:#}", "p{color:#zzz}", "p{color:rgb(}", "p{color:rgb(1,2)}",
                "p{width:}", "p{width:px}", "p{width:-5px}", "p{font-size:1e400px}"
            };

            var slow = new List<string>();
            var threw = new List<string>();

            foreach (var css in broken)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    new CssStylesheet().Parse(css);
                    watch.Stop();
                    if (watch.ElapsedMilliseconds > 1000) slow.Add(css);
                }
                catch (Exception e) { threw.Add(css + " -> " + e.GetType().Name); }
            }

            Check("malformed css never throws", threw.Count == 0, string.Join("; ", threw));
            Check("malformed css never stalls", slow.Count == 0, string.Join("; ", slow));

            Check("a null selector is refused, not fatal", CssSelector.Parse(null) == null);
            Check("an empty selector is refused, not fatal", CssSelector.Parse("") == null);
            Check("shorthand hex is understood", CssStylesheet.ParseColor("#f00").HasValue);
            Check("a named colour ignores case", CssStylesheet.ParseColor("RED").HasValue);
            Check("a nonsense colour is null", !CssStylesheet.ParseColor("notacolour").HasValue);
            Check("a nonsense length is null", !CssStylesheet.ParseLength("wide", 100f).HasValue);

            // At-rules: a screen query is flattened, everything else is
            // stepped over. A print-only block used to style the screen.
            var paragraph = HtmlParser.Parse("<p>hi</p>").Find("p");

            var printOnly = new CssStylesheet();
            printOnly.Parse("@media print { p { font-size: 99px } }");
            Check("print-only rules do not reach the screen",
                printOnly.StyleFor(paragraph).FontSize is not 99f);

            var screenOnly = new CssStylesheet();
            screenOnly.Parse("@media screen { p { font-size: 21px } }");
            Check("screen rules do apply", screenOnly.StyleFor(paragraph).FontSize is 21f,
                screenOnly.StyleFor(paragraph).FontSize?.ToString());

            var afterFontFace = new CssStylesheet();
            afterFontFace.Parse("@font-face { font-family: x; src: url(y) } p { font-size: 13px }");
            Check("rules after an at-rule still parse",
                afterFontFace.StyleFor(paragraph).FontSize is 13f);

            try
            {
                new CssStylesheet().Parse("@media print { p { color: red }");
                Check("an unterminated at-rule does not hang", true);
            }
            catch (Exception e) { Check("an unterminated at-rule does not hang", false, e.GetType().Name); }
        }

        // What a machine publishes: a walk of its own folder. The whole network rests on this - an unfound share is off the network with nothing to say why.
        static void TestShares()
        {
            s_log.Add("Shares");

            var root = Path.Combine(Path.GetTempPath(), "retroos-shares-" + Guid.NewGuid().ToString("N"));
            var host = EditorUtility.CreateGameObjectWithHideFlags(
                "RetroOS Share Probe", HideFlags.HideAndDontSave);
            var previousRoot = RetroStorage.OverrideRoot;

            try
            {
                // <root>/machines/probe/C            shared as DRIVE
                //                      /C/archive    shared as ARCHIVE, sealed
                //                      /C/a/b/deep   shared, but out of range
                //                      /C/twin       shared as ARCHIVE again
                var machineFolder = Path.Combine(root, RetroStorage.MachinesFolder, "probe");
                var c = Path.Combine(machineFolder, "C");
                var archive = Path.Combine(c, "archive");
                var deep = Path.Combine(c, "a", "b", "deep");
                var twin = Path.Combine(c, "twin");
                foreach (var folder in new[] { archive, deep, twin }) Directory.CreateDirectory(folder);

                new RetroMeta { Drive = "C", Share = "DRIVE" }.Write(c);
                new RetroMeta { Share = "ARCHIVE", SharePassword = "1958" }.Write(archive);
                new RetroMeta { Share = "DEEP" }.Write(deep);
                new RetroMeta { Share = "ARCHIVE" }.Write(twin);

                RetroStorage.OverrideRoot = root;

                var machine = host.AddComponent<RetroMachine>();
                var serialized = new SerializedObject(machine);
                serialized.FindProperty("machineName").stringValue = "PROBE";
                serialized.FindProperty("folderName").stringValue = "probe";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                machine.Notify();

                var shares = machine.Shares.ToList();
                var names = shares.Select(x => x.ShareName).OrderBy(n => n).ToList();

                Check("a drive folder that names a share is published",
                    names.Contains("DRIVE"), string.Join(", ", names));
                Check("and so is a folder inside it", names.Contains("ARCHIVE"));
                Check("a share deeper than the scan reaches is not",
                    !names.Contains("DEEP"), "found " + names.Count + ": " + string.Join(", ", names));
                Check("two folders cannot publish under one name",
                    names.Count(n => n == "ARCHIVE") == 1, string.Join(", ", names));

                // The duplicate-share clash is refused with a warning; clear it after so a re-read does not warn again in a customer's console.
                Directory.Delete(twin, true);
                File.Delete(RetroMeta.SidecarFor(twin));
                machine.Notify();

                var sealed_ = shares.FirstOrDefault(x => x.ShareName == "ARCHIVE");
                Check("a share carries its password", sealed_ is { RequiresPassword: true });
                Check("and points at its own folder",
                    sealed_ != null && sealed_.Folder.EndsWith("archive"), sealed_?.Folder);
                Check("a share is listed unless it says otherwise", sealed_ is { Visible: true });

                // Reachability is the machine's, not the share's: pull the plug
                // and nothing it publishes is on the network any more.
                machine.PoweredOn = false;
                Check("nothing a powered-off machine publishes is reachable",
                    machine.Shares.All(x => !x.Available));
                machine.PoweredOn = true;

                // The share list is cached (Network Neighborhood asks on every draw), so a sidecar edited behind its back needs a Notify to show up.
                _ = machine.Shares.Count();          // warm it
                var late = Path.Combine(c, "late");
                Directory.CreateDirectory(late);
                new RetroMeta { Share = "LATE" }.Write(late);
                Check("a folder shared behind the cache is not on the network yet",
                    !machine.Shares.Any(x => x.ShareName == "LATE"));
                machine.Notify();
                Check("and is the moment the machine re-reads",
                    machine.Shares.Any(x => x.ShareName == "LATE"));
            }
            catch (Exception e)
            {
                Check("share discovery does not throw", false, e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                RetroStorage.OverrideRoot = previousRoot;
                UnityEngine.Object.DestroyImmediate(host);
                try { Directory.Delete(root, true); }
                catch (Exception e)
                {
                    Debug.LogWarning("RetroOS self test: could not remove its temporary share " +
                                     $"folder '{root}'. {e.Message}");
                }
            }
        }

        static void TestStorage()
        {
            s_log.Add("Storage");

            var machine = RetroStorage.MachineFolder(RetroSampleContent.Workstation);
            Check("sample machine folder exists", Directory.Exists(machine), machine);
            if (!Directory.Exists(machine)) return;

            var drives = DiskMount.MountDrives(machine);
            Check("drives mounted", drives.Count >= 2, drives.Count + " found");

            var c = drives.FirstOrDefault(d => d.Letter == 'C');
            Check("C: is the hard disk", c is { Kind: DriveKind.Fixed }, c?.Kind.ToString());
            Check("C: carries its volume label", !string.IsNullOrEmpty(c?.Label), c?.Label);
            Check("C: is ready", c is { Ready: true });

            // The data root is wherever the game pointed it (a dropped share, no read permission); none of those may throw during boot.
            foreach (var bad in new[] { null, "", "Z:/no/such/machine" })
            {
                try { Check($"a bad machine folder mounts to nothing [{bad ?? "null"}]",
                            DiskMount.MountDrives(bad).Count == 0); }
                catch (Exception e)
                {
                    Check($"a bad machine folder mounts to nothing [{bad ?? "null"}]", false,
                        e.GetType().Name);
                }
            }

            var floppy = drives.FirstOrDefault(d => d.Letter == 'A');
            Check("A: is a floppy", floppy is { Kind: DriveKind.Floppy }, floppy?.Kind.ToString());
            Check("an empty floppy drive is not ready", floppy is { Ready: false });

            var users = c?.Root.Find("Users") as VfsDirectory;
            var home = users?.Find("operator") as VfsDirectory;
            var docs = home?.Find("Documents") as VfsDirectory;
            Check("per-user folders mounted", docs != null, "C:/Users/operator/Documents");

            // Through List: Children walks past private folders, and reading Content walks past the owner and password.
            var readme = docs?.List(true, null).OfType<VfsFile>()
                .FirstOrDefault(f => f.Name.EndsWith(".txt"));
            Check("a document is there", readme != null);
            Check("text loads lazily",
                readme != null && readme.TryRead(null, out var body) &&
                !string.IsNullOrEmpty(body));
            Check("file kind detected", readme?.Kind == FileKind.Text);
            Check("size reported", readme is { Size: > 0 });

            // The sidecar carries what the file system cannot.
            var server = RetroStorage.MachineFolder(RetroSampleContent.Server);
            var serverC = DiskMount.Mount(Path.Combine(server, "C"), "C:");
            var archive = serverC.Find("archive");
            Check("sidecar encrypts a folder", archive is { Encrypted: true });
            Check("sidecar sets the password", archive?.Password == "1958", archive?.Password);
            Check("sidecar is hidden from the listing",
                serverC.List(true, null).All(n => !n.Name.EndsWith(RetroMeta.Extension)));

            // --- who owns what ---------------------------------------

            var usersNode = users;
            Check("Users folder is recognised", usersNode?.Role == NodeRole.UserRoot,
                usersNode?.Role.ToString());
            Check("a home folder belongs to its user",
                home is { Role: NodeRole.UserHome } && home.Owner == "operator",
                $"{home?.Role} / {home?.Owner}");
            Check("Documents is recognised", docs?.Role == NodeRole.Documents, docs?.Role.ToString());
            Check("documents inherit the owner", docs?.Owner == "operator", docs?.Owner);
            Check("a file inherits the owner too", readme?.Owner == "operator", readme?.Owner);

            // The whole point: one user cannot list another's folder.
            var everyone = usersNode?.List(false).Count() ?? 0;
            var asOperator = usersNode?.List(false, "operator").Count() ?? 0;
            Check("an unrestricted listing sees every home", everyone >= 2, everyone.ToString());
            Check("a signed-in user sees only their own home", asOperator == 1, asOperator.ToString());

            var other = usersNode?.List(false).OfType<VfsDirectory>()
                .FirstOrDefault(d => d.Owner != "operator");
            Check("the other home exists to test against", other != null, other?.Name);
            Check("another user's home is not visible", other?.IsVisibleTo("operator") == false);
            Check("its owner can see it", other?.IsVisibleTo(other.Owner) == true);
            Check("nobody signed in sees everything", other?.IsVisibleTo(null) == true);

            var otherDocs = other?.Find("Documents") as VfsDirectory;
            var otherFile = otherDocs?.List(true, null).OfType<VfsFile>().FirstOrDefault();
            Check("a private file is owned", !string.IsNullOrEmpty(otherFile?.Owner), otherFile?.Owner);
            Check("a private file is invisible to the wrong user",
                otherFile?.IsVisibleTo("operator") == false);

            // System folders get their roles so the shell can icon them.
            Check("Windows is a system folder",
                (c?.Root.Find("Windows") as VfsDirectory)?.Role == NodeRole.System);
            Check("Program Files is recognised",
                (c?.Root.Find("Program Files") as VfsDirectory)?.Role == NodeRole.Programs);

            var meta = new RetroMeta { Hidden = true, Password = "x", Note = "hello" };
            Check("meta round-trips", meta.Hidden && meta.Password == "x" && meta.Note == "hello");
            meta.Hidden = false;
            Check("clearing a flag removes the key", !meta.Hidden);
        }

        // --- network -------------------------------------------------------

        static void TestNetwork()
        {
            s_log.Add("Network");

            var net = new NetworkService();
            var index = net.Request(RetroSampleContent.Site);
            Check("a folder under internet/ is published",
                index.Ok && index.FilePath.EndsWith("index.html"),
                index.Status + " " + index.FilePath);

            var page = net.Request(RetroSampleContent.Site + "/bulletin.html");
            Check("explicit page resolves", page.Ok, page.Status);

            var scheme = net.Request("http://" + RetroSampleContent.Site + "/index.html");
            Check("http:// prefix is stripped", scheme.Ok, scheme.Status);

            var relative = net.Request("bulletin.html", RetroSampleContent.Site, "");
            Check("relative link stays on the host",
                relative.Ok && relative.Host == RetroSampleContent.Site, relative.Status);

            var asset = net.Request("img/logo.png", RetroSampleContent.Site, "");
            Check("relative image resolves", asset.Ok && asset.Kind == FileKind.Image, asset.Status);

            var escape = net.Request(RetroSampleContent.Site + "/../../../secrets.txt");
            Check("path traversal is refused", !escape.Ok, escape.Status);

            var missing = net.Request("nowhere.invalid");
            Check("unknown host reports not found",
                !missing.Ok && missing.Status.Contains("not found"), missing.Status);

            net.Online = false;
            Check("offline blocks requests", !net.Request(RetroSampleContent.Site).Ok);
            net.Online = true;

            // Every link and asset the sample site names must resolve - the demo rotted once when its index referenced pages never copied in.
            TestSiteLinks(net, RetroSampleContent.Site);
            TestRulePaths();

            // Addresses come from links inside hand-written pages, so the
            // resolver has to cope with whatever an author typed.
            Check("null path normalizes to empty", NetworkService.Normalize(null) == "");
            Check("dot segments collapse", NetworkService.Normalize("a/b/../c") == "a/c",
                NetworkService.Normalize("a/b/../c"));
            Check("walking above the root stops at it",
                !NetworkService.Normalize("../../etc/passwd").StartsWith(".."),
                NetworkService.Normalize("../../etc/passwd"));

            foreach (var nasty in new[] { "", "   ", "///", "?", "#frag", "http://",
                                          "a?b=1#c", "HTTP://Host.Com/Page.HTML" })
            {
                try
                {
                    NetworkService.Split(nasty, RetroSampleContent.Site, "");
                    Check("address survives [" + nasty + "]", true);
                }
                catch (Exception e)
                {
                    Check("address survives [" + nasty + "]", false, e.GetType().Name);
                }
            }

            Check("normalise collapses ..", NetworkService.Normalize("a/b/../c") == "a/c",
                NetworkService.Normalize("a/b/../c"));

            // With no machines in the scene there is nothing on the LAN, which
            // is the point: shares come from objects, not from folders.
            Check("an empty scene has no neighbours", !net.Neighbours.Any());
            Check("the neighbourhood branch is empty too",
                !net.MountNeighborhood().List(true, null).Any());

            var loader = new WebPageLoader(net);
            var loaded = loader.Load(RetroSampleContent.Site);
            Check("page loads and parses", loaded.Ok && loaded.Document != null, loaded.Status);
            Check("linked stylesheet is pulled in", loaded.Style.RuleCount > 0,
                loaded.Style.RuleCount + " rules");

            var resolved = loader.Resolve(loaded, "img/logo.png");
            Check("relative url resolves against the page",
                resolved == RetroSampleContent.Site + "/img/logo.png", resolved);
        }

        // --- media -----------------------------------------------------------

        static void TestMedia()
        {
            s_log.Add("Media");

            var net = new NetworkService();
            var loader = new WebPageLoader(net);
            var image = RetroSampleContent.Site + "/img/logo.png";

            var response = net.Request(image);
            Check("a picture resolves", response.Ok, response.Status);
            Check("and is recognised as an image", response.Kind == FileKind.Image,
                response.Kind.ToString());

            // The bug this pins: a .png used to be read with File.ReadAllText
            // and shown as a page of binary.
            var page = loader.Load(image);
            Check("the browser gets a media page", page.Ok && page.IsMedia, page.Status);
            Check("it is not treated as html", !page.IsHtml);
            Check("it is not treated as text", !page.IsPlainText);
            Check("no binary is read as a string", string.IsNullOrEmpty(page.PlainText),
                page.PlainText?.Length.ToString());
            Check("the file is handed over instead", !string.IsNullOrEmpty(page.FilePath));
            Check("with its size", page.Size > 0, page.Size.ToString());
            Check("and the file name as the title", page.Title == "logo.png", page.Title);

            var sprite = MediaLoader.LoadSprite(page.FilePath);
            Check("the picture decodes", sprite != null);
            Check("at its real size", sprite != null && sprite.rect.width == 64,
                sprite?.rect.width.ToString());

            // Markup still parses the normal way.
            var html = loader.Load(RetroSampleContent.Site);
            Check("a page is still html", html.Ok && html.IsHtml && !html.IsMedia, html.Status);
            Check("and keeps its <title>", html.Title != null && html.Title.Contains("Northpoint"),
                html.Title);

            // An animated picture is a numbered sequence.
            var frames = loader.LoadFrames(RetroSampleContent.Site + "/img/works.1.png",
                RetroSampleContent.Site, "");
            Check("numbered frames animate", frames == null || frames.Count > 1,
                frames?.Count.ToString() ?? "none authored");
        }

        // --- cards ---------------------------------------------------------

        // --- lyrics --------------------------------------------------------

        static void TestLyrics()
        {
            s_log.Add("Lyrics");

            var lines = LrcLyrics.Parse(
                "[ti:A Song]" + Nl +
                "[00:01.50]First line" + Nl +
                "[00:12.00]Second line" + Nl +
                "[01:05.25]Third line" + Nl);

            Check("an id tag is not a lyric", lines.Count == 3, lines.Count + " lines");
            Check("a stamp becomes seconds", Math.Abs(lines[0].Time - 1.5f) < 0.01f,
                lines[0].Time.ToString());
            Check("a minute is sixty seconds", Math.Abs(lines[2].Time - 65.25f) < 0.01f,
                lines[2].Time.ToString());

            var chorus = LrcLyrics.Parse("[00:10.00][01:00.00]Chorus" + Nl);
            Check("one line under two stamps is two lines", chorus.Count == 2,
                chorus.Count + " lines");

            Check("nothing in, nothing out", LrcLyrics.Parse(null).Count == 0);
            Check("a line with no stamp is dropped",
                LrcLyrics.Parse("just words" + Nl).Count == 0);

            Check("before the first line", LrcLyrics.LineAt(lines, 0f) == -1);
            Check("on a stamp", LrcLyrics.LineAt(lines, 12f) == 1);
            Check("between two stamps holds the earlier",
                LrcLyrics.LineAt(lines, 11.9f) == 0);
            Check("past the end holds the last", LrcLyrics.LineAt(lines, 9999f) == 2);
        }

        static string Nl => "" + (char)10;

        static void TestCards()
        {
            s_log.Add("Cards");
            var deck = CardTable.NewDeck();
            Check("52 cards", deck.Count == 52, deck.Count.ToString());
            Check("no duplicates", deck.Distinct().Count() == 52);
            Check("13 of each suit",
                Enumerable.Range(0, 4).All(s => deck.Count(c => (int)c.Suit == s) == 13));
            Check("aces read as A", deck.First(c => c.Rank == 1).RankText == "A");
            Check("red suits flagged",
                deck.Count(c => c.Red) == 26, deck.Count(c => c.Red).ToString());

            // The undo trim both patience games share: it rebuilds a Stack to drop from the bottom, exactly the shape that silently keeps the wrong end.
            var undo = new Stack<int>();
            for (var i = 1; i <= 5; i++) undo.Push(i);

            CardTable.TrimUndo(undo, 5);
            Check("a stack at the limit is left alone", undo.Count == 5, undo.Count.ToString());

            CardTable.TrimUndo(undo, 3);
            Check("trimming drops to the limit", undo.Count == 3, undo.Count.ToString());
            var order = string.Join(",", undo);
            Check("and drops the oldest, not the newest", order == "5,4,3", order);

            CardTable.TrimUndo(undo, 0);
            Check("a limit of nothing empties it", undo.Count == 0, undo.Count.ToString());
            CardTable.TrimUndo(undo, 3);
            Check("and an empty stack is left alone", undo.Count == 0, undo.Count.ToString());
        }
    }
}
