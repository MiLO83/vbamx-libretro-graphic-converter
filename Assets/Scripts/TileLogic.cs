using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using Free4GameDevsX2;

public class TileLogic : MonoBehaviour
{
    public bool processingPNGs = false;
    public Text ProgressText;
    public Camera TileCam;
    public Camera SceneCam;
    public bool viewingScene = false;
    public int width = 29;
    public int height = 26;
    public int layers = 4;
    public int viewingLayer = 0;
    public GameObject TilePrefab;
    public GameObject PalettePrefab;
    public GameObject PaletteParent;
    public GameObject GraphicParent;
    public GameObject SceneParent;
    public int enhancement_multiplier = 4;
    public List<FileInfo> vramdumpfiles = new List<FileInfo>();
    public byte[] tileFile;
    public byte[] tileBytes;
    public byte[] paletteBytes = new byte[2048];
    public Color32[] paletteColors = new Color32[2048];
    public Color[] tileColorArray;
    public int[] tileColors;
    public FileInfo[] tileFiles;
    public static string tilePath = "";
    public int tileNumber = 0;
    public GameObject[,] paletteGO = new GameObject[16,16];
    public static List<GameObject> TileList = new List<GameObject>();
    public GameObject[] TileListArray = new GameObject[32 * 32 * 30];
    public static GameObject[,] TileGO = new GameObject[8, 8];
    public static Texture2D TileT2D;
    public bool playTiles = false;
    public int selected_pal_index = 0;
    public bool is256 = false;
    public int bpp8FileIndex = 0;
    public string[] vramdump;
    DirectoryInfo tileFolder;
    public Texture2D inputTexture;
    public Texture2D outputTexture;
    public PixelScanner pixelScanner;
    public Color currentInputRGB = new Color();
    public Color currentOutputRGB = new Color();
    public enum ScaleAmount { TwoTimes, FourTimes, EightTimes, SixteenTimes }
    public ScaleAmount scaleAmount;
    int palbank = 0;
    int pal = 0;
    public enum Approach { Traditional, Precise }

    private void MainPass(bool SoftenIt, Approach approach)
    {
        int totalPasses = 1;
        int texW = outputTexture.width;
        int texH = outputTexture.height;
        switch (approach)
        {
            case Approach.Traditional:
                for (int passNumber = 0; passNumber < totalPasses; passNumber++)
                {
                    for (int scnY = 0; scnY < texH; scnY++)
                    {
                        for (int scnX = 0; scnX < texW; scnX++)
                        {
                            pixelScanner.DoTraditionalPass(ref HSLColor.ColorsList, ref scnX, ref scnY, texW, texH, passNumber, false, false); //width is also passed to find x,y in a one dimensional array/list with equasion: x + (y * width)
                        }
                    }
                }
                break;
            case Approach.Precise:
                totalPasses = 6;
                for (int passNumber = 0; passNumber < totalPasses; passNumber++)
                {
                    for (int scnY = 0; scnY < texH; scnY++)
                    {
                        for (int scnX = 0; scnX < texW; scnX++)
                        {
                            pixelScanner.DoPrecisePasses(ref HSLColor.ColorsList, ref scnX, ref scnY, texW, texH, passNumber); //width is also passed to find x,y in a one dimensional array/list with equasion: x + (y * width)
                        }
                    }
                }
                break;
        }
    }

    private void UpscaleNearestNeighbor(ref Texture2D outputTexture, int ScaleUpBy)
    {
        for (int y = 0; y < outputTexture.height; y++)
        {
            for (int x = 0; x < outputTexture.width; x++)
            {
                //Input RGB
                currentInputRGB = inputTexture.GetPixel(x / ScaleUpBy, y / ScaleUpBy);
                //To Output RGB
                outputTexture.SetPixel(x, y, currentInputRGB);
            }
        }
        outputTexture.Apply();
    }

    private void ScalePass(int ScaleUpBy)
    {
        UpscaleNearestNeighbor(ref outputTexture, ScaleUpBy);
    }

    public Texture2D Upscale(Texture2D tile)
    {
        int timesRan = 0;
        for (int x = 0; x < 2; x++)
        {
            if (timesRan == 0)
            {
                inputTexture = tile;
                inputTexture.filterMode = FilterMode.Point;
            }
            else if (timesRan > 0)
            {
                inputTexture = outputTexture;
            }

            outputTexture = new Texture2D(inputTexture.width * 2, inputTexture.height * 2);
            outputTexture.filterMode = FilterMode.Point;

            pixelScanner = new PixelScanner(ref outputTexture);

            ScalePass(2);

            HSLColor.InitializeColorsList(outputTexture);

            MainPass(false, Approach.Precise); //< - Where everything happens.

            outputTexture = HSLColor.ApplyToTexture2D(outputTexture);
            outputTexture.Apply();

            timesRan++;

        }
        return outputTexture;

    }

    // Start is called before the first frame update
    void Start()
    {
        tileColorArray = new Color[(8 * enhancement_multiplier) * (8 * enhancement_multiplier)];
        SceneCam.enabled = false;
        TilePrefab.SetActive(true);
        PalettePrefab.SetActive(true);
        if (Application.platform != RuntimePlatform.Android)
        {
            tileFolder = new DirectoryInfo(Application.persistentDataPath + "/../../../Roaming/RetroArch/vbamx/");
        }
        else
        {
            tileFolder = new DirectoryInfo(Application.persistentDataPath); //tileFolder = new DirectoryInfo("/storage/emulated/0/RetroArch/vbam2x/");
        }
        FileInfo[] dumpfiles = tileFolder.GetFiles("*.vram", SearchOption.AllDirectories);
        for (int d = 0; d < dumpfiles.Length; d++)
        {
            Debug.Log("LOADED : " + dumpfiles[d].FullName);
            vramdump = File.ReadAllLines(dumpfiles[d].FullName);
            for (int l = 0; l < vramdump.Length; l++)
            {
                vramdumpfiles.Add(new FileInfo(dumpfiles[d].FullName.Replace("index.vram", "") + vramdump[l].Split(',')[0]));
            }
            
        }
        FileInfo[] testPNG = tileFolder.GetFiles("*.png", SearchOption.AllDirectories);
        if (testPNG.Length > 0)
        {
            processingPNGs = true;
        }
        //FileInfo[] tileFiles4bpp = tileFolder.GetFiles("*.4bpx", SearchOption.AllDirectories);
        //FileInfo[] tileFiles8bpp = tileFolder.GetFiles("*.8bpx", SearchOption.AllDirectories);
        tileFiles = new FileInfo[vramdumpfiles.Count]; //  + tileFiles4bpp.Length + tileFiles8bpp.Length]
        Array.Copy(vramdumpfiles.ToArray(), 0, tileFiles, 0, vramdumpfiles.Count);
        //Array.Copy(tileFiles4bpp, 0, tileFiles, vramdumpfiles.Count, tileFiles4bpp.Length);
        //Array.Copy(tileFiles8bpp, 0, tileFiles, vramdumpfiles.Count + tileFiles4bpp.Length, tileFiles8bpp.Length);
        //bpp8FileIndex = tileFiles4bpp.Length;

        
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {

                    paletteGO[x, y] = Instantiate(PalettePrefab, new Vector3(-1f + (1f / 16f * x), 0.5f - (1f / 16f * y), 0), Quaternion.identity);
                    paletteGO[x, y].transform.localScale = new Vector3(1f / 16f, 1f / 16f, 1f);
                    paletteGO[x, y].name = "Pal" + x + "_" + y;
                    paletteGO[x, y].tag = "Palette";
                    paletteGO[x, y].transform.SetParent(PaletteParent.transform);
                }
            }

            /*
            StartCoroutine(NextTile());
            } else {
                Debug.Log("Found no Tile files. :(");
            }
            */
            
                StartCoroutine(TilesToPNG());
            
        
    }

    private static int GetClosestColor(Color32[] colorArray, Color32 baseColor)
    {
        var colors = colorArray.Select(x => new { Value = x, Diff = GetDiff(x, baseColor) }).ToList();
        var min = colors.Min(x => x.Diff);
        var foundcolor = colors.Find(x => x.Diff == min).Value;
        bool found = false;
        int match = 0;
        for (int i = 0; i < colorArray.Length; i++)
        {
            if (colorArray[i].Equals(foundcolor))
            {
                found = true;
                match = i;

            }
        }
        if (found)
        {
            return match;
        } else
        {
            return 0;
        }
    }

    private static float GetDiff(Color32 color, Color32 baseColor)
    {
        int a = color.a - baseColor.a,
            r = color.r - baseColor.r,
            g = color.g - baseColor.g,
            b = color.b - baseColor.b;
        return a * a + r * r + g * g + b * b;
    }

    public IEnumerator TilesToPNG()
    {
        List<string> processedFiles = new List<string>();
        TilePrefab.SetActive(true);
        PalettePrefab.SetActive(true);
        if (!processingPNGs)
        {
            Debug.Log("Converting Tiles to PNGs...");
        } else
        {
            Debug.Log("Converting PNGs to Tiles...");
        }
        foreach (Transform child in SceneParent.transform)
        {
            GameObject.Destroy(child.gameObject);
        }
        
        for (int vdf = 0; vdf < vramdumpfiles.Count; vdf++)
        {
            if (vdf % 10 == 0)
            {
                yield return new WaitForEndOfFrame();
                ProgressText.text = (vdf + 2) + "/" + vramdumpfiles.Count;
            }
                bool foundPNG = false;
                bool inScene = false;
                bool found = false;
                string tileName = "";

            if (!processedFiles.Contains(vramdumpfiles[vdf].FullName))
            {
                if (File.Exists(vramdumpfiles[vdf].FullName))
                {



                    if (vramdump[vdf] != null && vramdump[vdf].Split(',').Length > 0)
                    {
                        found = true;

                    if (!File.Exists(vramdumpfiles[vdf].FullName.Replace(".4bpx", ".4bpp").Replace(".8bpx", ".8bpp")))
                    {
                        tileFile = File.ReadAllBytes(vramdumpfiles[vdf].FullName);
                        //Debug.Log("Loaded Tile : " + vramdumpfiles[vdf].FullName);
                        tileName = vramdumpfiles[vdf].FullName;

                    }
                    else
                    {
                        tileFile = File.ReadAllBytes(vramdumpfiles[vdf].FullName.Replace(".4bpx", ".4bpp").Replace(".8bpx", ".8bpp"));
                        //Debug.Log("Loaded Tile : " + vramdumpfiles[vdf].FullName.Replace(".4bpx", ".4bpp").Replace(".8bpx", ".8bpp") + " and loaded EDITED Tile :)");
                        tileName = vramdumpfiles[vdf].FullName.Replace(".4bpx", ".4bpp").Replace(".8bpx", ".8bpp");

                    }
                    processedFiles.Add(vramdumpfiles[vdf].FullName);
                    
                        if (vramdump[vdf].Split(',')[1] == "256")
                        { //
                            is256 = true;
                            pal = 0;
                            palbank = 0;

                        }
                        else if (vramdump[vdf].Split(',')[1] == "257")
                        { //
                            is256 = true;
                            pal = 0;
                            palbank = 1;

                        }
                        else
                        {

                            is256 = false;
                            int.TryParse(vramdump[vdf].Split(',')[1], out pal);
                            palbank = 0;
                        }

                        if (pal > 16)
                        {
                            found = false;
                        }
                    } else
                    {
                        found = false;
                    }
                    if (found)
                    {
                        Texture2D T2D = new Texture2D((8 * enhancement_multiplier), (8 * enhancement_multiplier));
                        Texture2D T2D8 = new Texture2D(8, 8);

                        int t = 0;
                        //TilePrefab.SetActive(true);
                        /*
                        for (int iy = 0; iy < 8; iy++) { 
                            for (int y = 0; y < enhancement_multiplier; y++) {         
                                for (int ix = 0; ix < 8; ix++) {
                                    for (int x = 0; x < enhancement_multiplier; x++) {
                                    //TileGO[((ix*enhancement_multiplier)+x), ((iy*enhancement_multiplier) + y)] = Instantiate(TilePrefab, new Vector3(0f + ((1f/ ((8f * (enhancement_multiplier))) * ((ix * enhancement_multiplier) + x)) + (sx)), 0.5f - ((1f/ ((8f * (enhancement_multiplier))) * ((iy * enhancement_multiplier) + y)) + sy), 0), Quaternion.identity);
                                    //TileGO[((ix*enhancement_multiplier)+x), ((iy*enhancement_multiplier) + y)].transform.localScale = new Vector3(1f/(8f * enhancement_multiplier), 1f/(8f * enhancement_multiplier), 1f);

                                    t++;
                                    }
                                }
                            }
                        }
                        */
                        if (File.Exists(vramdumpfiles[vdf].FullName.Replace(".4bpx", ".png").Replace(".8bpx", ".png")))
                        {
                            T2D.LoadImage(File.ReadAllBytes(vramdumpfiles[vdf].FullName.Replace(".4bpx", ".png").Replace(".8bpx", ".png")));
                            foundPNG = true; // SET TO TRUE!!
                        }
                        int b = 0;
                        tileBytes = new byte[tileFile.Length - 1024];
                        for (int i = 0; i < tileFile.Length - 1024; i++)
                        {
                            tileBytes[i] = tileFile[i];
                        }
                        for (int i = tileFile.Length - 1024; i < tileFile.Length; i++)
                        {
                            paletteBytes[b++] = tileFile[i];
                        }
                        for (int p = 0; p < 512; p++)
                        {
                            if (p * 2 < paletteBytes.Length - 1)
                            {
                                short color = BitConverter.ToInt16(paletteBytes, ((p * 2)));
                                paletteColors[p].r = (byte)((color & 0x1f) << 3);
                                paletteColors[p].g = (byte)(((color >> 5) & 0x1f) << 3);
                                paletteColors[p].b = (byte)(((color >> 10) & 0x1f) << 3);
                                paletteColors[p].a = 255;
                            }
                        }
                        for (int y = 0; y < 16; y++)
                        {
                            for (int x = 0; x < 16; x++)
                            {

                                paletteGO[x, y].GetComponent<Renderer>().material.color = paletteColors[(y * 16) + x];
                                paletteGO[x, y].name = (x + (y * 16)).ToString();
                            }
                        }

                        string log = "";
                        string val = "";
                        int px = 0;
                        int ii = 0;
                        bool hi_low_switch = true;
                        int fin = 0;
                        for (int y = 0; y < enhancement_multiplier; y++)
                        {
                            for (int x = 0; x < enhancement_multiplier; x++)
                            {
                                for (int iy = 0; iy < 8; iy++)
                                {
                                    for (int ix = 0; ix < 8; ix++)
                                    {
                                        if (!is256)
                                        {

                                            ii = ((64 * fin) + (iy * 4 + (ix >> 1)));
                                            if (tileBytes.Length > ii)
                                            {
                                                /*    
                                                byte[] ca = new byte[2];
                                                int count = 0;

                                                    if (hi_low_switch) {
                                                    if (tileBytes.Length > iy * 4 + (ix >> 1))
                                                    ca[1] = tileBytes[iy * 4 + (ix >> 1)];

                                                    if (tileBytes.Length > iy * 4 + (ix >> 1) + 1) 
                                                    ca[0] = tileBytes[iy * 4 + (ix >> 1) + 1];
                                                    } else {
                                                        if (tileBytes.Length > iy * 4 + (ix >> 1) + 1)
                                                    ca[1] = tileBytes[iy * 4 + (ix >> 1) + 1];

                                                    if (tileBytes.Length > iy * 4 + (ix >> 1)) 
                                                    ca[0] = tileBytes[iy * 4 + (ix >> 1)];
                                                    }
                                                    //hi_low_switch = !hi_low_switch;
                                                    //}   
                                                    */
                                                if (hi_low_switch)
                                                {
                                                    if (!foundPNG)
                                                    {
                                                        int c = tileBytes[ii] & 0x0f; //(ushort)BitConverter.ToUInt16(ca, 0);
                                                        log += " " + tileBytes[ii].ToString("X2");//6010060
                                                                                                  //log  += " " + tileBytes[ii+1].ToString("X2");//6010060
                                                        val += " " + c.ToString();
                                                        ushort color = (ushort)BitConverter.ToUInt16(paletteBytes, (pal * 16) + c);

                                                        //Debug.Log(tileBytes[(y * (8 * enhancement_multiplier)) + (x*2)]);
                                                        if (c == 0)
                                                        {

                                                            T2D8.SetPixel(ix, iy, Color.magenta);
                                                            //TileListArray[((sl*(width * height)) + (sy * width) + sx)].name += c.ToString() + ",";
                                                        }
                                                        else
                                                        {
                                                            Color32 clr = new Color32((byte)((color & 0x1f) << 3), (byte)(((color >> 5) & 0x1f) << 3), (byte)(((color >> 10) & 0x1f) << 3), 1);
                                                            T2D8.SetPixel(ix, iy, paletteColors[(palbank * 256) + (pal * 16) + (int)c]); //clr;//
                                                                                                                                                                                                          //TileListArray[((sl*(width * height)) + (sy * width) + sx)].name += c.ToString() + ",";
                                                                                                                                                                                                          //Tile[((ix*enhancement_multiplier)+x), ((iy*enhancement_multiplier) + y)].
                                                        }
                                                        px++;

                                                    }
                                                    else // foundTGA
                                                    {
                                                        Color32[] smallPal = new Color32[16];
                                                        Array.Copy(paletteColors, pal * 16, smallPal, 0, 16);
                                                        smallPal[0] = Color.magenta;
                                                        int nibble1 = GetClosestColor(smallPal, T2D.GetPixel(((ix * enhancement_multiplier) + x), ((iy * enhancement_multiplier) + y))) % 16;
                                                        int nibble2 = (tileFile[ii] >> 4) & 0x0f;

                                                        tileFile[ii] = (byte)(nibble1 | (nibble2 << 4));
                                                        log += " " + tileFile[ii].ToString("X2");//6010060
                                                                                                 //log  += " " + tileBytes[ii+1].ToString("X2");//6010060


                                                        px++;
                                                    }

                                                }
                                                else
                                                {

                                                    if (!foundPNG)
                                                    {

                                                        int c = (tileBytes[ii] >> 4) & 0x0f; //(ushort)BitConverter.ToUInt16(ca, 0);
                                                        log += " " + tileBytes[ii].ToString("X2");//6010060
                                                                                                  //log  += " " + tileBytes[ii+1].ToString("X2");//6010060
                                                        val += " " + c.ToString();
                                                        ushort color = (ushort)BitConverter.ToUInt16(paletteBytes, (pal * 16) + c);

                                                        //Debug.Log(tileBytes[(y * (8 * enhancement_multiplier)) + (x*2)]);
                                                        if (c == 0)
                                                        {
                                                            T2D8.SetPixel(ix, iy, Color.magenta);
                                                            //TileListArray[((sl*(width * height)) + (sy * width) + sx)].name += c.ToString() + ",";
                                                        }
                                                        else
                                                        {
                                                            Color32 clr = new Color32((byte)((color & 0x1f) << 3), (byte)(((color >> 5) & 0x1f) << 3), (byte)(((color >> 10) & 0x1f) << 3), 1);
                                                            T2D8.SetPixel(ix, iy, paletteColors[(palbank * 256) + (pal * 16) + (int)c]); //clr;//
                                                                                                                                                                                                          //TileListArray[((sl*(width * height)) + (sy * width) + sx)].name += c.ToString() + ",";
                                                        }
                                                    }
                                                    else // foundTGA
                                                    {
                                                        Color32[] smallPal = new Color32[16];
                                                        Array.Copy(paletteColors, pal * 16, smallPal, 0, 16);
                                                        smallPal[0] = Color.magenta;
                                                        int nibble1 = tileFile[ii] & 0x0f;
                                                        int nibble2 = GetClosestColor(smallPal, T2D.GetPixel(((ix * enhancement_multiplier) + x), ((iy * enhancement_multiplier) + y))) % 16;


                                                        tileFile[ii] = (byte)(nibble1 | (nibble2 << 4));
                                                        log += " " + tileFile[ii].ToString("X2");//6010060
                                                                                                 //log  += " " + tileBytes[ii+1].ToString("X2");//6010060
                                                    }

                                                }
                                                hi_low_switch = !hi_low_switch;

                                            }
                                            if (ix == 7 && iy == 7)
                                            {
                                                fin++;
                                            }


                                        }
                                        else
                                        {

                                            ii = ((64 * fin) + (iy * 8 + (ix)));
                                            if (tileBytes.Length > ii)
                                            {

                                                if (!foundPNG)
                                                {

                                                    int c = tileBytes[ii];
                                                    log += " " + tileBytes[ii].ToString("X2");//6010060
                                                                                              //log  += " " + tileBytes[ii+1].ToString("X2");//6010060
                                                    val += " " + c.ToString();
                                                    ushort color = (ushort)BitConverter.ToUInt16(paletteBytes, c);

                                                    //Debug.Log(tileBytes[(y * (8 * enhancement_multiplier)) + (x*2)]);
                                                    if (c == 0)
                                                    {
                                                        T2D8.SetPixel(ix, iy, Color.magenta);
                                                        //TileListArray[((sl*(width * height)) + (sy * width) + sx)].name += c.ToString() + ",";
                                                    }
                                                    else
                                                    {
                                                        if (px < paletteColors.Length)
                                                        {
                                                            Color32 clr = new Color32((byte)((color & 0x1f) << 3), (byte)(((color >> 5) & 0x1f) << 3), (byte)(((color >> 10) & 0x1f) << 3), 1);
                                                            T2D8.SetPixel(ix, iy, paletteColors[(256 * palbank) + (int)c]);

                                                            //TileListArray[((sl*(width * height)) + (sy * width) + sx)].name += c.ToString() + ",";
                                                        }
                                                    }
                                                    px++;


                                                }
                                                else // foundTGA
                                                {
                                                    Color32[] smallPal = new Color32[256];
                                                    Array.Copy(paletteColors, palbank * 256, smallPal, 0, 256);
                                                    smallPal[0] = Color.magenta;
                                                    tileFile[ii] = (byte)GetClosestColor(smallPal, T2D.GetPixel(((ix * enhancement_multiplier) + x), ((iy * enhancement_multiplier) + y)));
                                                    log += " " + tileFile[ii].ToString("X2");//6010060
                                                                                             //log  += " " + tileBytes[ii+1].ToString("X2");//6010060


                                                    px++;
                                                }

                                            }



                                            if (ix == 7 && iy == 7)
                                            {
                                                fin++;
                                            }
                                        }

                                    }

                                }



                            }

                        }
                        if (!foundPNG)
                        {
                            T2D8.Apply();
                            T2D8.filterMode = FilterMode.Point;
                            T2D8.wrapMode = TextureWrapMode.Clamp;
                            Texture2D T2D8Up = Upscale(T2D8);
                            if (!File.Exists(vramdumpfiles[vdf].FullName.Replace(".4bpx", ".png").Replace(".8bpx", ".png")))
                            {
                                if (!vramdumpfiles[vdf].FullName.Replace(".4bpx", ".png").Replace(".8bpx", ".png").Contains("f5a5fd42d16a20302798ef6ed309979b43003d2320d9f0e8ea9831a92759fb4b"))
                                {
                                    byte[] bytes = T2D8Up.EncodeToPNG();

                                    File.WriteAllBytes(vramdumpfiles[vdf].FullName.Replace(".4bpx", ".png").Replace(".8bpx", ".png"), bytes);
                                    Debug.Log("SAVED PNG : " + vramdumpfiles[vdf].FullName.Replace(".4bpx", ".png").Replace(".8bpx", ".png"));
                                }
                            }
                        }
                        else
                        {
                            if (processingPNGs)
                            {
                                if (tilePath == "")
                                {
                                    //File.WriteAllBytes(vramdumpfiles[vdf].FullName, tileFile);
                                    if (!is256)
                                    {
                                        File.WriteAllBytes(vramdumpfiles[vdf].FullName.Replace("4bpx", "4bpp"), tileFile);
                                    }
                                    else
                                    {
                                        File.WriteAllBytes(vramdumpfiles[vdf].FullName.Replace("8bpx", "8bpp"), tileFile);
                                    }
                                }
                                else
                                {
                                    //File.WriteAllBytes(vramdumpfiles[vdf].FullName, tileFile);
                                    if (!is256)
                                    {
                                        File.WriteAllBytes(vramdumpfiles[vdf].FullName.Replace("4bpx", "4bpp"), tileFile);
                                    }
                                    else
                                    {
                                        File.WriteAllBytes(vramdumpfiles[vdf].FullName.Replace("8bpx", "8bpp"), tileFile);
                                    }
                                }
                                Debug.Log("SAVED BPP : " + vramdumpfiles[vdf].FullName.Replace(".4bpx", ".4bpp").Replace(".8bpx", ".8bpp"));
                            }
                        }

                        //Debug.Log(log);
                        log = "";
                        //Debug.Log(val);
                        val = "";

                    }

                }
            }
                
            
        }
        TilePrefab.SetActive(false);
        PalettePrefab.SetActive(false);
        Debug.Log("-DONE- :) :)");
        ProgressText.text = "-DONE- :) :)";
        Application.Quit();
    }
}
