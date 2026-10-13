using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 3WL - Three Wrestling Leagues: thumb, arm and amateur wrestling.
// Each match = a league bout you play (mash SPACE / click), then a scripted
// STAGE WRESTLING bout, performed like a movie, with the SAME result as the league bout.
// Builds itself at runtime: just press Play.
public class ThreeWL : MonoBehaviour
{
    private enum State { Title, Intro, Fight, Break, Stage, Result }
    private enum After { Menu, Retry, Next }

    private class Fig
    {
        public RectTransform legs, body, head, arm1, arm2;
        public float s;
    }

    private static readonly string[] League = { "THUMB WL", "ARM WL", "AMATEUR WL" };
    private static readonly string[] Word = { "THUMB", "ARM", "AMATEUR" };
    private static readonly string[] Opp = { "Thumbelina Tom", "Bruno the Biceps", "Singlet Sam" };
    private static readonly string[] StageOpp = { "Captain Thumbslam", "Lumberjack Lou", "El Gigante" };
    private static readonly string[] Finisher = { "THUMB SPLASH", "BICEP BOMB", "DOUBLE-LEG SLAM" };
    private static readonly Color[] LeagueBg =
    {
        new Color(0.12f, 0.22f, 0.42f),
        new Color(0.32f, 0.2f, 0.1f),
        new Color(0.12f, 0.3f, 0.18f)
    };
    private static readonly Color Skin = new Color(1f, 0.8f, 0.62f);
    private static readonly Color SkinDark = new Color(0.9f, 0.68f, 0.5f);
    private static readonly Color Blue = new Color(0.2f, 0.4f, 0.95f);
    private static readonly Color Red = new Color(0.9f, 0.15f, 0.15f);
    private static readonly Color LegColor = new Color(0.15f, 0.15f, 0.2f);

    private const float BarWidth = 700f;
    private const float PressGain = 2.2f;
    private const float FloorY = -185f;
    private const float StageLength = 15.5f;

    private static bool booted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        booted = false;
    }

    private static readonly string[][] Sub =
    {
        new[] { "LEFT THUMB", "RIGHT THUMB", "TIE-BREAKER THUMB" },
        new[] { "LEFT ARM", "RIGHT ARM", "TIE-BREAKER ARM" },
        new[] { "BACK PIN", "FRONT PIN", "TIE-BREAKER: BACK OR FRONT PIN" }
    };
    private const float TieBreakTime = 7f;
    private int bout, pWins, oWins;
    private bool tieBreak, seriesDone;
    private float boutTimer;

    private State state;
    private After after;
    private int league;
    private bool career;
    private bool playerWon;
    private float meter = 50f;
    private float stateTimer;
    private float stageT;
    private float shake;
    private float pulse;
    private float surgeLeft;
    private float nextSurge;

    private RectTransform canvasRt;
    private RectTransform arena;
    private RectTransform barFill;
    private GameObject barGroup;
    private Image bgImage;
    private Text sideText, titleText, progressText, playerName, oppText, prompt, center, surgeText;
    private Action<float> animate;
    private Fig pFig, oFig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!booted)
        {
            booted = true;
            new GameObject("3WL").AddComponent<ThreeWL>();
        }
    }

    private void Start()
    {
        BuildUI();
        ShowTitle();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool press = (kb != null && kb.spaceKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        bool confirm = kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
        bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Title:
                if (kb == null) break;
                if (kb.digit1Key.wasPressedThisFrame) StartMatch(0, false);
                else if (kb.digit2Key.wasPressedThisFrame) StartMatch(1, false);
                else if (kb.digit3Key.wasPressedThisFrame) StartMatch(2, false);
                else if (confirm) StartMatch(0, true);
                break;

            case State.Intro:
                stateTimer -= dt;
                center.text = stateTimer > 1.6f ? "READY?" : stateTimer > 0.8f ? "SET..." : "FIGHT!";
                UpdateVisuals();
                if (stateTimer <= 0f)
                {
                    center.text = "";
                    state = State.Fight;
                }
                break;

            case State.Fight:
                RunFight(press, dt);
                break;

            case State.Break:
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    if (seriesDone) StartStage(); else StartBout();
                }
                break;

            case State.Stage:
                stageT += dt;
                AnimateStage(stageT);
                if (stageT >= StageLength || press)
                {
                    FinishStage();
                }
                break;

            case State.Result:
                if (esc)
                {
                    ShowTitle();
                }
                else if (confirm || press)
                {
                    if (after == After.Next)
                    {
                        league++;
                        StartRound();
                    }
                    else if (after == After.Retry)
                    {
                        StartRound();
                    }
                    else
                    {
                        ShowTitle();
                    }
                }
                break;
        }
    }

    private void RunFight(bool press, float dt)
    {
        if (press)
        {
            meter += PressGain;
            shake = 1f;
            pulse = 1f;
        }

        float push = 10f + league * 3f + (tieBreak ? 4f : 0f);
        if (tieBreak)
        {
            boutTimer -= dt;
            prompt.text = $"TIE-BREAKER: WIN FAST! {Mathf.Max(0f, boutTimer):0.0}s";
        }
        nextSurge -= dt;
        if (surgeLeft > 0f)
        {
            surgeLeft -= dt;
            push *= 2.2f;
        }
        else if (nextSurge <= 0f)
        {
            surgeLeft = 0.8f;
            nextSurge = UnityEngine.Random.Range(3f, 5f);
        }

        meter = Mathf.Clamp(meter - push * dt, 0f, 100f);
        shake = Mathf.MoveTowards(shake, 0f, dt * 6f);
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 5f);
        surgeText.text = surgeLeft > 0f ? "OPPONENT SURGE! MASH!" : "";
        UpdateVisuals();

        if (meter >= 100f)
        {
            EndLeagueBout(true);
        }
        else if (meter <= 0f || (tieBreak && boutTimer <= 0f))
        {
            EndLeagueBout(false);
        }
    }

    private void UpdateVisuals()
    {
        barFill.sizeDelta = new Vector2(BarWidth * meter / 100f, 30f);
        float c = (meter - 50f) * 5f + shake * UnityEngine.Random.Range(-8f, 8f);
        animate?.Invoke(c);

        prompt.enabled = state == State.Fight;
        prompt.transform.localScale = Vector3.one * (1f + pulse * 0.3f);
        prompt.color = new Color(1f, 1f, 0.3f, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f)));
    }

    private void StartMatch(int startLeague, bool careerMode)
    {
        league = startLeague;
        career = careerMode;
        StartRound();
    }

    private void StartRound()
    {
        pWins = 0;
        oWins = 0;
        bout = 0;
        StartBout();
    }

    private void StartBout()
    {
        meter = 50f;
        shake = 0f;
        pulse = 0f;
        surgeLeft = 0f;
        nextSurge = 3.5f;
        state = State.Intro;
        stateTimer = 2.4f;
        tieBreak = bout == 2;
        boutTimer = TieBreakTime;

        barGroup.SetActive(true);
        playerName.text = "YOU";
        oppText.text = Opp[league];
        titleText.text = League[league] + ": " + Sub[league][bout];
        sideText.text = string.Join("\n", Word[league].ToCharArray()) + "\n\nW\nL";
        string leagueTag = career ? $"League {league + 1} of 3  -  " : "";
        progressText.text = leagueTag + $"Score {pWins}-{oWins}. Win 2 bouts, then the scripted stage match copies the result!";
        prompt.text = tieBreak ? "TIE-BREAKER: WIN FAST! MASH!" : "MASH SPACE (or click) FAST!";
        surgeText.text = "";
        center.text = "";

        ClearArena();
        center.fontSize = 64;
        bgImage.color = LeagueBg[league];
        if (league == 0)
        {
            BuildThumb();
        }
        else if (league == 1)
        {
            BuildArm();
        }
        else
        {
            BuildMat();
        }

        UpdateVisuals();
    }

    private void EndLeagueBout(bool won)
    {
        if (won) pWins++; else oWins++;
        surgeText.text = "";
        state = State.Break;
        stateTimer = 3.2f;
        bool decided = pWins == 2 || oWins == 2 || bout == 2;
        string label = Sub[league][bout];

        if (!decided)
        {
            seriesDone = false;
            bout++;
            center.text = (won ? $"{label} WON!" : $"{label} LOST!") + $"\nScore {pWins}-{oWins}\n" +
                (bout == 2 ? "Tied! Tie-breaker: WIN FAST!" : $"Next: {Sub[league][bout]}");
            stateTimer = 2.6f;
            return;
        }

        seriesDone = true;
        playerWon = pWins > oWins;
        center.text = playerWon
            ? $"{League[league]} WON {pWins}-{oWins}!\nNOW THE STAGE WRESTLING MATCH!\nScripted like a movie: you win it too!"
            : $"You lost {League[league]} {pWins}-{oWins}...\nNOW THE STAGE WRESTLING MATCH!\nScripted like a movie: same result.";
    }

    private void StartStage()
    {
        state = State.Stage;
        stageT = 0f;
        center.text = "";
        surgeText.text = "";
        barGroup.SetActive(false);
        playerName.text = "YOU";
        oppText.text = StageOpp[league];
        titleText.text = "STAGE WRESTLING MATCH";
        progressText.text = "Bout 2 of 2: a movie-style scripted match. SPACE skips";
        prompt.enabled = true;
        prompt.transform.localScale = Vector3.one;
        prompt.color = Color.white;

        ClearArena();
        bgImage.color = new Color(0.12f, 0.05f, 0.2f);
        BuildStage();
        AnimateStage(0f);
    }

    private void FinishStage()
    {
        arena.anchoredPosition = Vector2.zero;
        state = State.Result;
        center.text = "";
        prompt.enabled = true;
        prompt.transform.localScale = Vector3.one;
        prompt.color = Color.yellow;

        if (!playerWon)
        {
            center.text = "You lost the match!\nThe stage match followed the same script.";
            prompt.text = "SPACE = rematch    ESC = menu";
            after = After.Retry;
        }
        else if (career && league < 2)
        {
            center.text = $"MATCH WON!\nNext league: {League[league + 1]}";
            prompt.text = "Press SPACE to continue";
            after = After.Next;
        }
        else if (career)
        {
            center.text = "3WL CHAMPION!\nYou conquered all three leagues!";
            prompt.text = "Press SPACE for the title screen";
            after = After.Menu;
        }
        else
        {
            center.text = "YOU WIN THE MATCH!";
            prompt.text = "Press SPACE for the title screen";
            after = After.Menu;
        }
    }

    private void ShowTitle()
    {
        state = State.Title;
        ClearArena();
        arena.anchoredPosition = Vector2.zero;
        animate = null;
        bgImage.color = new Color(0.08f, 0.06f, 0.18f);
        barGroup.SetActive(false);
        titleText.text = "3WL";
        sideText.text = "";
        progressText.text = "THREE WRESTLING LEAGUES";
        playerName.text = "";
        oppText.text = "";
        surgeText.text = "";
        center.text =
            "[ENTER / SPACE]  Career: beat all 3 leagues\n\n" +
            "[1] Thumb WL\n[2] Arm WL\n[3] Amateur WL\n\n" +
            "Every match = a league bout you play (mash SPACE)\n" +
            "+ a scripted STAGE WRESTLING bout with the SAME result.";
        center.fontSize = 32;
        prompt.text = "";
        prompt.enabled = false;
    }

    private void ClearArena()
    {
        foreach (Transform t in arena)
        {
            Destroy(t.gameObject);
        }
    }

    private void BuildThumb()
    {
        Rect(arena, "Table", new Vector2(0, -250), new Vector2(1400, 300), new Color(0.45f, 0.28f, 0.14f));
        Rect(arena, "HandP", new Vector2(-430, -170), new Vector2(320, 190), Skin);
        Rect(arena, "CuffP", new Vector2(-610, -170), new Vector2(80, 190), Blue);
        Rect(arena, "HandO", new Vector2(430, -170), new Vector2(320, 190), SkinDark);
        Rect(arena, "CuffO", new Vector2(610, -170), new Vector2(80, 190), Red);
        RectTransform pt = Rect(arena, "ThumbP", Vector2.zero, Vector2.one, Skin);
        RectTransform ot = Rect(arena, "ThumbO", Vector2.zero, Vector2.one, SkinDark);

        animate = c =>
        {
            Limb(pt, new Vector2(-300f, -130f), new Vector2(c * 0.6f - 25f, 60f), 75f);
            Limb(ot, new Vector2(300f, -130f), new Vector2(c * 0.6f + 25f, 60f), 75f);
        };
    }

    private void BuildArm()
    {
        Rect(arena, "Table", new Vector2(0, -250), new Vector2(1000, 220), new Color(0.5f, 0.3f, 0.15f));
        Rect(arena, "PadP", new Vector2(-170, -150), new Vector2(120, 30), new Color(0.2f, 0.2f, 0.25f));
        Rect(arena, "PadO", new Vector2(170, -150), new Vector2(120, 30), new Color(0.2f, 0.2f, 0.25f));
        RectTransform pa = Rect(arena, "ArmP", Vector2.zero, Vector2.one, Skin);
        RectTransform ps = Rect(arena, "SleeveP", Vector2.zero, Vector2.one, Blue);
        RectTransform oa = Rect(arena, "ArmO", Vector2.zero, Vector2.one, SkinDark);
        RectTransform os = Rect(arena, "SleeveO", Vector2.zero, Vector2.one, Red);
        RectTransform clasp = Rect(arena, "Clasp", Vector2.zero, new Vector2(100f, 100f), Skin);

        animate = c =>
        {
            Vector2 pe = new Vector2(-170f, -135f);
            Vector2 oe = new Vector2(170f, -135f);
            Vector2 h = new Vector2(c * 0.5f, 110f - Mathf.Abs(c) * 0.35f);
            Limb(pa, pe, h, 80f);
            Limb(ps, pe, Vector2.Lerp(pe, h, 0.4f), 92f);
            Limb(oa, oe, h, 80f);
            Limb(os, oe, Vector2.Lerp(oe, h, 0.4f), 92f);
            clasp.anchoredPosition = h;
        };
    }

    // Amateur wrestling: two wrestlers pushing on a mat; the contact point follows the meter.
    private void BuildMat()
    {
        Rect(arena, "Wall", new Vector2(0, 120), new Vector2(1400, 300), new Color(0.2f, 0.28f, 0.22f));
        Rect(arena, "Mat", new Vector2(0, -270), new Vector2(1400, 200), new Color(0.3f, 0.55f, 0.9f));
        Rect(arena, "MatLine", new Vector2(0, -175), new Vector2(1400, 8), Color.white);

        Fig p = MakeFig(Blue, Skin, Skin, 1f);
        Fig o = MakeFig(new Color(0.95f, 0.55f, 0.1f), SkinDark, SkinDark, 1f);

        animate = c =>
        {
            float lean = (meter - 50f) * 0.3f;
            Place(p, c - 100f, 0f, Mathf.Clamp(lean, 0f, 12f), 25f, 1f);
            Place(o, c + 100f, 0f, Mathf.Clamp(-lean, 0f, 12f), 25f, -1f);
        };
    }

    private void BuildStage()
    {
        Rect(arena, "Floor", new Vector2(0, -270), new Vector2(1400, 200), new Color(0.22f, 0.1f, 0.32f));
        Rect(arena, "Spot", new Vector2(0, 100), new Vector2(500, 700), new Color(1f, 1f, 0.7f, 0.07f));
        Color[] rope = { Red, Color.white, Blue };
        for (int i = 0; i < 3; i++)
        {
            Rect(arena, "Rope", new Vector2(0, 40 + i * 70), new Vector2(1100, 10), rope[i]);
        }

        Rect(arena, "PostL", new Vector2(-550, 60), new Vector2(20, 320), new Color(0.7f, 0.7f, 0.7f));
        Rect(arena, "PostR", new Vector2(550, 60), new Vector2(20, 320), new Color(0.7f, 0.7f, 0.7f));
        for (int i = 0; i < 31; i++)
        {
            Color crowd = Color.HSVToRGB(UnityEngine.Random.value, 0.5f, 0.7f);
            Rect(arena, "Fan", new Vector2(-620 + i * 42, 285 + UnityEngine.Random.Range(0f, 25f)), new Vector2(30, 30), crowd);
        }

        pFig = MakeFig(Blue, Skin, Skin, 1f);
        oFig = MakeFig(Red, SkinDark, new Color(0.15f, 0.15f, 0.15f), 1.3f);
    }

    private Fig MakeFig(Color bodyColor, Color armColor, Color headColor, float s)
    {
        var f = new Fig { s = s };
        f.legs = Rect(arena, "Legs", Vector2.zero, new Vector2(100f, 70f) * s, LegColor);
        f.body = Rect(arena, "Body", Vector2.zero, new Vector2(120f, 190f) * s, bodyColor);
        f.head = Rect(arena, "Head", Vector2.zero, new Vector2(80f, 80f) * s, headColor);
        f.arm1 = Rect(arena, "Arm1", Vector2.zero, new Vector2(90f, 36f) * s, armColor);
        f.arm2 = Rect(arena, "Arm2", Vector2.zero, new Vector2(90f, 36f) * s, armColor);
        f.arm1.pivot = new Vector2(0f, 0.5f);
        f.arm2.pivot = new Vector2(0f, 0.5f);
        return f;
    }

    // x: feet position. fall: degrees tipped backwards (0 standing, 90 lying). armAng: arm angle above horizontal.
    // dir: +1 faces right, -1 faces left.
    private static void Place(Fig f, float x, float lift, float fall, float armAng, float dir)
    {
        float rot = dir * fall;
        float rad = rot * Mathf.Deg2Rad;
        Vector2 pivot = new Vector2(x, FloorY + lift + 60f * f.s * Mathf.Abs(Mathf.Sin(rad)));
        float armZ = dir > 0f ? armAng : 180f - armAng;

        Put(f.legs, pivot, new Vector2(0f, 35f), rot, dir, f.s, 0f);
        Put(f.body, pivot, new Vector2(0f, 165f), rot, dir, f.s, 0f);
        Put(f.head, pivot, new Vector2(8f, 300f), rot, dir, f.s, 0f);
        Put(f.arm1, pivot, new Vector2(35f, 215f), rot, dir, f.s, armZ);
        Put(f.arm2, pivot, new Vector2(35f, 170f), rot, dir, f.s, armZ);
    }

    private static void Put(RectTransform rt, Vector2 pivot, Vector2 offset, float rot, float dir, float s, float extraZ)
    {
        Vector2 o = new Vector2(offset.x * dir, offset.y) * s;
        float rad = rot * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        rt.anchoredPosition = pivot + new Vector2(o.x * cs - o.y * sn, o.x * sn + o.y * cs);
        rt.localRotation = Quaternion.Euler(0f, 0f, rot + extraZ);
    }

    // The scripted match. The winner (whoever won the league bout) always wins it too.
    private void AnimateStage(float t)
    {
        Fig w = playerWon ? pFig : oFig;
        Fig l = playerWon ? oFig : pFig;
        float wd = playerWon ? 1f : -1f;
        float ld = -wd;
        float wBase = playerWon ? -110f : 110f;
        float lBase = -wBase;
        string winner = playerWon ? "YOU" : StageOpp[league];
        string loser = playerWon ? StageOpp[league] : "YOU";

        float wx = wBase + wd * 50f, lx = lBase + ld * 50f;
        float wLift = 0f, lLift = 0f, wFall = 0f, lFall = 0f, wArm = 30f, lArm = 30f;
        float quake = 0f;
        string cap = "";
        string big = "";

        float lyingX = lBase + wd * 20f;
        float pinX = lyingX + wd * 110f;
        float pinLift = 120f * l.s;

        if (t < 2f)
        {
            float sway = Mathf.Sin(t * 5f) * 15f;
            wx = wBase + wd * 50f + sway;
            lx = lBase + ld * 50f + sway;
            wArm = lArm = 50f;
            cap = "Lock-up! The crowd goes wild!";
        }
        else if (t < 4f)
        {
            float k = Mathf.Abs(Mathf.Sin(t * 12f));
            wArm = 20f + 50f * k;
            lx += wd * 10f * k;
            lFall = 10f * k;
            cap = "CHOP! CHOP! CHOP!";
        }
        else if (t < 6f)
        {
            float k = Mathf.Abs(Mathf.Sin(t * 12f));
            lArm = 20f + 50f * k;
            wx += ld * 10f * k;
            wFall = 10f * k;
            cap = $"REVERSAL! {loser} fights back!";
        }
        else if (t < 8f)
        {
            float u = (t - 6f) / 2f;
            float rel = u < 0.5f ? Mathf.Lerp(50f, -60f, u * 2f) : Mathf.Lerp(-60f, 110f, (u - 0.5f) * 2f);
            wx = wBase + wd * rel;
            wArm = u < 0.5f ? 30f : 0f;
            cap = u < 0.5f ? "Whip to the ropes!" : $"{winner} charges back!";

            float f = Mathf.Clamp01((u - 0.8f) / 0.2f);
            lFall = Mathf.Lerp(0f, 90f, f);
            lx = lBase + ld * 50f + wd * 70f * f;
            if (f > 0f)
            {
                big = "CLOTHESLINE!!";
                quake = f > 0.5f ? 0.8f : 0f;
            }
        }
        else if (t < 10f)
        {
            float v = (t - 8f) / 2f;
            lx = lyingX;
            lFall = 90f;
            float startX = wBase + wd * 110f;
            if (v < 0.5f)
            {
                wx = Mathf.Lerp(startX, startX - wd * 50f, v * 2f);
                wArm = 100f;
                cap = $"{winner} gets ready for the finisher...";
            }
            else
            {
                float a = (v - 0.5f) * 2f;
                wx = Mathf.Lerp(startX - wd * 50f, pinX, a);
                wLift = 170f * Mathf.Sin(Mathf.PI * a) + (a > 0.9f ? pinLift : 0f);
                wFall = a > 0.7f ? Mathf.Lerp(0f, 90f, (a - 0.7f) / 0.3f) : 0f;
                wArm = 120f;
                cap = $"{winner} flies!";
                if (a > 0.9f)
                {
                    big = $"THE {Finisher[league]}!";
                    quake = 1f;
                }
            }
        }
        else if (t < 13f)
        {
            lx = lyingX;
            lFall = 90f;
            wx = pinX;
            wFall = 90f;
            wLift = pinLift;
            cap = "The pin!";
            if (t >= 12.2f)
            {
                big = "3!!";
                cap = "DING DING DING!";
            }
            else if (t >= 11.4f)
            {
                big = "2...";
            }
            else if (t >= 10.6f)
            {
                big = "1...";
            }
        }
        else
        {
            float k = Mathf.Clamp01((t - 13f) / 0.6f);
            lx = lyingX;
            lFall = 90f;
            wx = pinX;
            wFall = Mathf.Lerp(90f, 0f, k);
            wLift = Mathf.Lerp(pinLift, 0f, k);
            wArm = 100f + 15f * Mathf.Sin(t * 8f);
            cap = $"WINNER: {winner}! (Scripted: same result as your league bout.)";
        }

        Place(w, wx, wLift, wFall, wArm, wd);
        Place(l, lx, lLift, lFall, lArm, ld);

        arena.anchoredPosition = quake > 0f ? UnityEngine.Random.insideUnitCircle * quake * 14f : Vector2.zero;
        center.fontSize = 72;
        center.text = big;
        prompt.text = cap;
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)canvasGo.transform;

        bgImage = Rect(canvasRt, "Bg", Vector2.zero, new Vector2(3000f, 2000f), Color.black).GetComponent<Image>();
        arena = Rect(canvasRt, "Arena", Vector2.zero, Vector2.zero, Color.clear);

        barGroup = new GameObject("Bar", typeof(RectTransform));
        barGroup.transform.SetParent(canvasRt, false);
        Rect((RectTransform)barGroup.transform, "Border", new Vector2(0f, 240f), new Vector2(BarWidth + 12f, 42f), Color.white);
        Rect((RectTransform)barGroup.transform, "Opp", new Vector2(0f, 240f), new Vector2(BarWidth, 30f), Red);
        barFill = Rect((RectTransform)barGroup.transform, "Fill", new Vector2(-BarWidth / 2f, 240f), new Vector2(BarWidth / 2f, 30f), new Color(0.2f, 0.9f, 0.3f));
        barFill.pivot = new Vector2(0f, 0.5f);

        sideText = MakeText("Side", new Vector2(-590f, -20f), new Vector2(100f, 600f), 38, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.3f));
        titleText = MakeText("Title", new Vector2(0f, 325f), new Vector2(1100f, 60f), 46, TextAnchor.MiddleCenter, Color.white);
        progressText = MakeText("Progress", new Vector2(0f, 287f), new Vector2(1100f, 40f), 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
        playerName = MakeText("Player", new Vector2(-350f, 200f), new Vector2(300f, 40f), 28, TextAnchor.MiddleCenter, new Color(0.5f, 0.8f, 1f));
        oppText = MakeText("Opp", new Vector2(350f, 200f), new Vector2(400f, 40f), 28, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.5f));
        surgeText = MakeText("Surge", new Vector2(0f, 200f), new Vector2(500f, 40f), 28, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.2f));
        center = MakeText("Center", new Vector2(0f, 20f), new Vector2(1200f, 400f), 64, TextAnchor.MiddleCenter, Color.white);
        prompt = MakeText("Prompt", new Vector2(0f, -320f), new Vector2(1200f, 60f), 36, TextAnchor.MiddleCenter, Color.yellow);
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private static void Limb(RectTransform r, Vector2 a, Vector2 b, float thickness)
    {
        Vector2 d = b - a;
        r.anchoredPosition = (a + b) / 2f;
        r.sizeDelta = new Vector2(d.magnitude, thickness);
        r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    private Text MakeText(string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(canvasRt, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        go.GetComponent<Outline>().effectColor = Color.black;
        return t;
    }
}
