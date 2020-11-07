using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BungieARG
{
    public partial class BungieARGForm : Form
    {
        private List<MyPoint> points = new List<MyPoint>();
        private ConcurrentDictionary<string, int> codes = new ConcurrentDictionary<string, int>();
        private SortedList<int, Tuple<int, int, string>> sentence = new SortedList<int, Tuple<int, int, string>>();

        public BungieARGForm()
        {
            InitializeComponent();
        }

        private void BungieARGForm_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {


            ThreadPool.SetMaxThreads(32, 10);
            try
            {
                HttpClient client = new HttpClient();

                if (File.Exists("images.txt")) {
                    var names = File.ReadAllLines("images.txt");

                    if (!Directory.Exists("images")) Directory.CreateDirectory("images");
                    
                    foreach(string s in names) {
                        ThreadPool.QueueUserWorkItem((object obj) => {
                            Uri u = new Uri((string)((object[])obj)[1]);
                            var result = ((HttpClient)((object[])obj)[0]).GetAsync((string)((object[])obj)[1]);
                            var response = result.Result;
                            response.EnsureSuccessStatusCode();

                            using (var fs = new FileStream(Path.Combine("images", Path.GetFileName(u.LocalPath)), FileMode.Create)) {
                                response.Content.CopyToAsync(fs).Wait();
                            }

                            Console.WriteLine("Finished reading: " + Path.Combine("images", Path.GetFileName(u.LocalPath)));
                        }, new object[]{ client, s});
                    }

                    bool working = true;
                    int workerThreads = 0;
                    int completionPortThreads = 0;
                    int maxWorkerThreads = 0;
                    int maxCompletionPortThreads = 0;

                    ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxCompletionPortThreads);

                    while (working) {
                        ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);

                        working = workerThreads != maxWorkerThreads;

                        Thread.Sleep(100);
                    }

                    Console.WriteLine("Finished reading files");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var files = Directory.GetFiles("images");

            string[] newFiles = new string[files.Length];

            for (var z = 0; z < newFiles.Length; z++)
            {
                newFiles[z] = files[z];
            }

            ThreadPool.SetMaxThreads(32, 10);

            foreach (string file in files) {

                ThreadPool.QueueUserWorkItem((object obj) => {

                    var i = Image.FromFile((string)((object[])obj)[0]);
                    var count = i.GetFrameCount(FrameDimension.Time);
                    var times = i.GetPropertyItem(0x5100).Value;
                    var slTime = new SortedList<int, int>();
                    var slCol = new SortedList<int, Color>();

                    for (int x = 0; x < count; x++) {
                        i.SelectActiveFrame(FrameDimension.Time, x);
                        if (x == 0) slTime.Add(x, BitConverter.ToInt32(times, 4 * x));
                        else slTime.Add(x, slTime[x - 1] + BitConverter.ToInt32(times, 4 * x));

                        Bitmap b = new Bitmap(i);

                        slCol.Add(x, b.GetPixel(1, 1));
                    }

                    //var col = slCol.First();
                    points.Add(new MyPoint() { _slTime = slTime, _slCol = slCol });

                    var filename = Path.Combine("data", Path.GetFileNameWithoutExtension((string)((object[])obj)[0]) + ".dat");
                    if (!Directory.Exists("data")) Directory.CreateDirectory("Data");
                    if (!File.Exists(filename)) {
                        StringBuilder sb = new StringBuilder();
                        for (int z = 0; z < slTime.Count; z++) {
                            sb.AppendLine($"{ slTime[z].ToString()},{slCol[z].ToString()}");
                        }
                        File.WriteAllText(filename, sb.ToString());
                    }

                    Console.WriteLine("Finished writing: " + Path.Combine("data", Path.GetFileNameWithoutExtension((string)((object[])obj)[0]) + ".dat"));
                }, new object[1] { file });

            }

            bool working = true;
            int workerThreads = 0;
            int completionPortThreads = 0;
            int maxWorkerThreads = 0;
            int maxCompletionPortThreads = 0;

            ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxCompletionPortThreads);

            while (working) {
                ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);

                working = workerThreads != maxWorkerThreads;

                Thread.Sleep(100);
            }

            Console.WriteLine("Finished processing data");

        }

        private async void button3_Click(object sender, EventArgs e)
        {

            var time = 0;
            if (!Directory.Exists("image2")) Directory.CreateDirectory("image2");

            while (time < 50000)
            {
                Bitmap b = new Bitmap(48, 44);
                int count = 0;
                for (int y = 0; y < 44; y++)
                {
                    for (int x = 0; x < 48; x++)
                    {


                        count = ((y) * 44) + x;
                        if (count < points.Count())
                        {

                            b.SetPixel(x, y, points[count].GetColor(time*40));
                            b.SetPixel(x, y, points[count].GetColor(int.Parse(textBox1.Text) * 40));
                            //textBox1.Text = time.ToString();

                            //                if(points[count].GetColor(int.Parse(textBox1.Text)*40) == Color.FromArgb(255,0,0,0))
                            //b.Save(Path.Combine("image2", time.ToString() + ".bmp"));
                        }
                    }
                }

                pictureBox1.Image = b;
                pictureBox1.Refresh();
                time += 1;
                await Task.Delay(10);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            points.Clear();
            var files = Directory.GetFiles("data");

            List<FileInfo> fileInfos = new List<FileInfo>();
            foreach (var file in files) {
                fileInfos.Add(new FileInfo(file));
            }
            files = fileInfos.OrderBy(fi => fi.Name).Select(f=>f.FullName).ToArray();

            //var tmp = files.ToList();
            //tmp.Sort();
            //files = tmp.ToArray();
            Parallel.ForEach(files, file =>
            {

                var vals = File.ReadAllLines(file);
                var slTime = new SortedList<int, int>();
                var slCol = new SortedList<int, Color>();

                for (int i = 0; i < vals.Count(); i++)
                {
                    var data = vals[i].Replace("Color [A=", "").Replace(" R=", "").Replace(" G=", "").Replace(" B=", "").Replace("]", "");
                    var pieces = data.Split(',');

                    slTime.Add(i,int.Parse( pieces[0]));
                    slCol.Add(i, Color.FromArgb( int.Parse(pieces[1]), int.Parse(pieces[2]), int.Parse(pieces[3]), int.Parse(pieces[4])));
                    
                }
                points.Add(new MyPoint() { _slCol = slCol, _slTime = slTime });

            });

        }

        private void button5_Click(object sender, EventArgs e)
        {
            //var time = 40;
            //StringBuilder sb = new StringBuilder();

            //var iter = 1650;
            //while (iter < 1750) {
            //    iter++;
            //    if (points.All(p => p.GetColor(iter * 40) == Color.FromArgb(255,255,255,255))) {
            //        sb.Append(iter);
            //        sb.Append(",");
            //    }
            //    time += 40;
            //}
            //textBox1.Text = sb.ToString();
            sentence.Clear();
            

            var iter = 0;

            ThreadPool.SetMaxThreads(32, 10);

            //StringBuilder code = new StringBuilder();
            for (iter = 0; iter < 50000; iter++) {

                /*Thread testThread = new Thread(new ThreadStart(() => {
                    StringBuilder code = new StringBuilder();
                    for (var num = 0; num < points.Count(); num++) {
                        code.Append(points[num].GetColor(iter * 40) == Color.FromArgb(255, 255, 255, 255) ? "1" : "0");
                    }
                    var codeNum = 0;
                    if (codes.ContainsValue(code.ToString())) codeNum = codes.Where(c => c.Value == code.ToString()).First().Key;
                    else {
                        codeNum = codes.Count();
                        codes.Add(codes.Count(), code.ToString());
                    }
                    if (codes.ContainsValue(code.ToString())) codeNum = codes.Where(c => c.Value == code.ToString()).First().Key;
                    else {
                        codeNum = codes.Count();
                        codes.Add(codes.Count(), code.ToString());
                    }
                    sentence.Add(new Tuple<int, int, string>(iter, codeNum, "_"));
                }));*/

                /*WaitCallback callback = new WaitCallback((object obj) => {
                    Console.WriteLine("Creating thread");
                    runningThreads++;
                    StringBuilder code = new StringBuilder();
                    for (var num = 0; num < points.Count(); num++) {
                        code.Append(points[num].GetColor(iter * 40) == Color.FromArgb(255, 255, 255, 255) ? "1" : "0");
                    }
                    var codeNum = 0;
                    if (codes.ContainsKey(code.ToString())) codeNum = codes[code.ToString()];
                    else {
                        codeNum = codes.Count();
                        codes.TryAdd(code.ToString(), codes.Count());
                    }
                    lock (sentence) {
                        sentence.Add(new Tuple<int, int, string>(iter, codeNum, "_"));
                    }
                    runningThreads--;
                    Console.WriteLine("Closing thread");
                });

                //callback.*/

                ThreadPool.QueueUserWorkItem((object obj) => {
                    StringBuilder code = new StringBuilder();
                    for (var num = 0; num < points.Count(); num++) {
                        code.Append(points[num].GetColor((int)(((object[])obj)[0]) * 40) == Color.FromArgb(255, 255, 255, 255) ? "1" : "0");
                    }

                    Console.WriteLine(code.ToString());

                    var codeNum = 0;

                    if (codes.ContainsKey(code.ToString())) codeNum = codes[code.ToString()];
                    else {
                        codeNum = codes.Count();
                        lock (codes) {
                            codes.TryAdd(code.ToString(), codes.Count());
                        }
                    }

                    lock (sentence) {
                        sentence.Add((int)(((object[])obj)[0]), new Tuple<int, int, string>((int)(((object[])obj)[0]), codeNum, "_"));
                    }

                    Console.WriteLine("Closeing Thread: " + (int)(((object[])obj)[0]));

                }, new object[1] { iter });

                //testThread.Start();

                //code = new StringBuilder();

                //for (var num = 0; num < points.Count(); num++) {
                //    code.Append(points[num].GetColor(iter * 40) == Color.FromArgb(255, 255, 255, 255) ? "1" : "0");
                //}

                //var codeNum = 0;

                //if (codes.ContainsValue(code.ToString())) codeNum = codes.Where(c => c.Value == code.ToString()).First().Key;
                //else {
                //    codeNum = codes.Count();
                //    codes.Add(codes.Count(), code.ToString());
                //}



                //sentence.Add(new Tuple<int, int, string>(iter, codeNum, "_"));

            }

            bool working = true;
            int workerThreads = 0;
            int completionPortThreads = 0;
            int maxWorkerThreads = 0;
            int maxCompletionPortThreads = 0;

            ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxCompletionPortThreads);

            while(working) {
                ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);

                working = workerThreads != maxWorkerThreads;

                Thread.Sleep(100);
            }

            StringBuilder final = new StringBuilder();

            foreach (KeyValuePair<int, Tuple<int, int, string>> tuple in sentence) {
                final.Append(tuple.Value.Item1).Append(",").Append(tuple.Value.Item2).Append(",").Append(tuple.Value.Item3).AppendLine();
            }

            File.WriteAllText("output.txt", final.ToString());

            final.Clear();

            SortedList<int, string> codesSorted = new SortedList<int, string>();

            foreach(string key in codes.Keys) {
                codesSorted.Add(codes[key], key);
            }

            Console.WriteLine("Length of sorted list: " + codesSorted.Count);
            
            foreach(KeyValuePair<int, string> value in codesSorted) {
                final.AppendLine($"{value.Key},{value.Value}");
            }

            File.WriteAllText("codes.txt", final.ToString());
        }
    }

    public class MyPoint
    {
        public SortedList<int, int> _slTime = new SortedList<int, int>();
        public SortedList<int, Color> _slCol = new SortedList<int, Color>();


        public Color GetColor(int time) {
            for (int i = 0; i < _slTime.Count(); i++) { 
                if(time < _slTime[i])
                {
                    if (i == 0) return _slCol[0];
                    return _slCol[i];
                }
            }
            return _slCol[_slCol.Keys.Last()];
        }

    }
}
