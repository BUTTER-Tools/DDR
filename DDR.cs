using System.Text;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using PluginContracts;
using OutputHelperLib;
using System.IO;


namespace DDRplugin
{
    public class DDRplugin : Plugin
    {


        public string[] InputType { get; } = { "Tokens" };
        public string OutputType { get; } = "OutputArray";

        public Dictionary<int, string> OutputHeaderData { get; set; } = new Dictionary<int, string>() { { 0, "TokenizedText" } };
        public bool InheritHeader { get; } = false;

        #region Plugin Details and Info

        public string PluginName { get; } = "Distributed Dictionary Coding";
        public string PluginType { get; } = "Language Analysis";
        public string PluginVersion { get; } = "1.0.2";
        public string PluginAuthor { get; } = "Ryan L. Boyd (ryan@ryanboyd.io)";
        public string PluginDescription { get; } = "Using a pre-trained word embedding model, the Distributed Dictionary Representation (DDR) method will calculate the similarity between each text and groups of words defined by the user. Very useful for \"fuzzy\" dictionary-like scoring of texts, particularly short text. Based on the method described in:" + Environment.NewLine + Environment.NewLine +
            "Garten, J., Hoover, J., Johnson, K. M., Boghrati, R., Iskiwitch, C., & Dehghani, M. (2017). Dictionaries and distributions: Combining expert knowledge and large scale textual data content analysis. Behavior Research Methods, 1–18. https://doi.org/10.3758/s13428-017-0875-9" + Environment.NewLine + Environment.NewLine +
            "Note that your seed word list is case sensitive, and this method cannot score texts for seed words that do not exist in your pre-trained model.";
        public bool TopLevel { get; } = false;
        public string PluginTutorial { get; } = "https://youtu.be/lmG-6EDiQlE";

        private double[][] model { get; set; }
        private int TotalNumRows { get; set; }
        private bool modelHasHeader { get; set; }


        public Icon GetPluginIcon
        {
            get
            {
                return Properties.Resources.icon;
            }
        }

        #endregion



        private string IncomingTextLocation { get; set; } = "";
        private string SelectedEncoding { get; set; } = "utf-8";
        private int VocabSize { get; set; } = 0;
        private int VectorSize { get; set; } = 0;
        private string[] WordList { get; set; } = { "authority, obey, respect, tradition",
                                                    "subversion, disobey, disrespect, chaos",
                                                    "kindness, compassion, nurture, empathy",
                                                    "suffer, cruel, hurt, harm",
                                                    "loyal, solidarity, patriot, fidelity",
                                                    "cheat, fraud, unfair, injustice",
                                                    "fairness, equality, justice, rights",
                                                    "betray, treason, disloyal, traitor",
                                                    "purity, sanctity, sacred, wholesome",
                                                    "impurity, depravity, degradation, unnatural"};
        private Dictionary<string, List<int>> ListOfAllWords { get; set; }
        private Dictionary<int, int> NumberOfWordsInGroup { get; set; }
        private Dictionary<int, double[]> WordGroupVectors { get; set; }
        private Dictionary<string, int> WordToArrayMap { get; set; }


        public void ChangeSettings()
        {

            using (var form = new SettingsForm_DDRplugin(IncomingTextLocation, SelectedEncoding, VectorSize, VocabSize, WordList))
            {


                form.Icon = Properties.Resources.icon;
                form.Text = PluginName;


                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    SelectedEncoding = form.SelectedEncoding;
                    IncomingTextLocation = form.InputFileName;
                    VocabSize = form.VocabSize;
                    VectorSize = form.VectorSize;
                    WordList = form.WordList;
                }
            }

        }




        //not used
        public Payload RunPlugin(Payload Input)
        {
            Payload pData = new Payload();
            pData.FileID = Input.FileID;
            pData.SegmentID = Input.SegmentID;

            for (int i = 0; i < Input.StringArrayList.Count; i++)
            {

                string[] OutputArray = new string[NumberOfWordsInGroup.Keys.Count];
                for (int j = 0; j < NumberOfWordsInGroup.Keys.Count; j++) OutputArray[j] = "";

                double[] textVector = new double[VectorSize];
                for (int j = 0; j < VectorSize; j++) textVector[j] = 0;

                int NumberOfDetectedWords = 0;

                //tally up an average vector for the text
                #region get mean text vector
                for(int tokenNumber = 0; tokenNumber < Input.StringArrayList[i].Length; tokenNumber++)
                {

                    if (WordToArrayMap.ContainsKey(Input.StringArrayList[i][tokenNumber]))
                    {
                        double[] detectedVec = model[WordToArrayMap[Input.StringArrayList[i][tokenNumber]]];
                        textVector = textVector.Zip(detectedVec, (x, y) => x + y).ToArray();
                        NumberOfDetectedWords++;
                    }

                }

                if (NumberOfDetectedWords > 0)
                {
                    for (int j = 0; j < VectorSize; j++) textVector[j] = textVector[j] / NumberOfDetectedWords;
                }
                #endregion


                #region Calculate Cosine Similarities
                if (NumberOfDetectedWords > 0)
                {
                    //calculate cosine Similarities
                    for (int wordlist_counter = 0; wordlist_counter < WordGroupVectors.Keys.Count; wordlist_counter++)
                    {

                        //https://janav.wordpress.com/2013/10/27/tf-idf-and-cosine-similarity/
                        //Cosine Similarity (d1, d2) =  Dot product(d1, d2) / ||d1|| * ||d2||
                        //
                        //Dot product (d1,d2) = d1[0] * d2[0] + d1[1] * d2[1] * … * d1[n] * d2[n]
                        //||d1|| = square root(d1[0]2 + d1[1]2 + ... + d1[n]2)
                        //||d2|| = square root(d2[0]2 + d2[1]2 + ... + d2[n]2)
                        double dotproduct = 0;
                        double d1 = 0;
                        double d2 = 0;

                        //calculate cosine similarity components
                        for (int j = 0; j < VectorSize; j++)
                        {
                            dotproduct += WordGroupVectors[wordlist_counter][j] * textVector[j];
                            d1 += WordGroupVectors[wordlist_counter][j] * WordGroupVectors[wordlist_counter][j];
                            d2 += textVector[j] * textVector[j];
                        }

                        OutputArray[wordlist_counter] = (dotproduct / (Math.Sqrt(d1) * Math.Sqrt(d2))).ToString();

                    }
                }
                #endregion
                pData.SegmentNumber.Add(Input.SegmentNumber[i]);
                pData.StringArrayList.Add(OutputArray);

            }

            return (pData);
        }





        public void Initialize()
        {

            OutputHeaderData = new Dictionary<int, string>();
            ListOfAllWords = new Dictionary<string, List<int>>();
            NumberOfWordsInGroup = new Dictionary<int, int>();
            WordGroupVectors = new Dictionary<int, double[]>();
            TotalNumRows = 0;







            #region initialize word lists
            string[] WordGroups = WordList.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            for (int i = 0; i < WordGroups.Length; i++)
            {
                
                //set up the header with the info here
                OutputHeaderData.Add(i, WordGroups[i].Trim());

                //split into individual words
                string[] IndividualWords = WordGroups[i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                //for each word, add it to the appropriate dictionary...
                //and make sure that it is mapped to all word groups to which it belongs
                foreach (string Word in IndividualWords)
                {
                    string WordClean = Word.Trim();
                    if (!ListOfAllWords.ContainsKey(WordClean))
                    {
                        ListOfAllWords.Add(WordClean, new List<int> { i });
                    }
                    else
                    {
                        ListOfAllWords[WordClean].Add(i);
                    }
                    
                }

                //track the number of found words in the group
                NumberOfWordsInGroup.Add(i, 0);

                //lastly, initialize the vector for the word group
                WordGroupVectors.Add(i, new double[VectorSize]);
                for (int j = 0; j < VectorSize; j++) WordGroupVectors[i][j] = 0;

            }
            #endregion



            //we could use a List<double[]> to load in the word vectors, then
            //just .ToArray() it to make jagged arrays. However, I *really* want to avoid
            //having to hold the model in memory twice
            WordToArrayMap = new Dictionary<string, int>();
            if (VocabSize != -1) model = new double[VocabSize][];

            try
            {

           



                #region capture dictionary words and initialize model, if vocabsize is known
                //now, during initialization, we actually go through and want to establish the word group vectors
                using (var stream = File.OpenRead(IncomingTextLocation))
                using (var reader = new StreamReader(stream, encoding: Encoding.GetEncoding(SelectedEncoding)))
                {

                    if (VocabSize != -1)
                    {
                        string[] firstLine = reader.ReadLine().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    int WordsFound = 0;
                    int NumWords = ListOfAllWords.Keys.Count;

                    while (!reader.EndOfStream)
                    {

                    
                        string line = reader.ReadLine().TrimEnd();
                        string[] splitLine = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        string RowWord = splitLine[0].Trim();
                        double[] RowVector = new double[VectorSize];
                        for (int i = 0; i < VectorSize; i++) RowVector[i] = Double.Parse(splitLine[i + 1]);

                        if (!WordToArrayMap.ContainsKey(RowWord))
                        {
                            WordToArrayMap.Add(RowWord, TotalNumRows);
                            if (VocabSize != -1) model[TotalNumRows] = RowVector;
                        }

                        //if the word is one that we want to capture, then we pull out the info that we want
                        if (ListOfAllWords.ContainsKey(RowWord))
                        {
                            WordsFound++;
                            for (int i = 0; i < ListOfAllWords[RowWord].Count; i++) NumberOfWordsInGroup[ListOfAllWords[RowWord][i]] += 1;
                            for (int i = 0; i < ListOfAllWords[RowWord].Count; i++)
                            {
                                for (int j = 0; j < VectorSize; j++)
                                {
                                    WordGroupVectors[ListOfAllWords[RowWord][i]][j] += RowVector[j];
                                }
                            }
                        
                        }

                        TotalNumRows++;

                    }
                }


                //last, we convert the word group vectors from sums into averages
                for (int i = 0; i < WordGroups.Length; i++)
                {
                    for (int j = 0; j < VectorSize; j++)
                    {
                        WordGroupVectors[i][j] = WordGroupVectors[i][j] / NumberOfWordsInGroup[i];
                    }
                }
                #endregion



                //if we didn't know the vocab size initially, we know it now that we've walked the whole model
                #region if vocab size was unknown, now we load up the whole model into memory
                if (VocabSize == -1)
                {
                    model = new double[TotalNumRows][];
                    TotalNumRows = 0;

                    //now, during initialization, we actually go through and want to establish the word group vectors
                    using (var stream = File.OpenRead(IncomingTextLocation))
                    using (var reader = new StreamReader(stream, encoding: Encoding.GetEncoding(SelectedEncoding)))
                    {

                        while (!reader.EndOfStream)
                        {


                            string line = reader.ReadLine().TrimEnd();
                            string[] splitLine = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            string RowWord = splitLine[0].Trim();
                            double[] RowVector = new double[VectorSize];
                            for (int i = 0; i < VectorSize; i++) RowVector[i] = Double.Parse(splitLine[i + 1]);

                            if (WordToArrayMap.ContainsKey(RowWord))
                            {
                                model[TotalNumRows] = RowVector;
                            }

                            TotalNumRows++;

                        }
                    }
                }
                    #endregion





            }
            catch (OutOfMemoryException OOM)
            {
                MessageBox.Show("Plugin Error: Distributed Dictionary. This plugin encountered an \"Out of Memory\" error while trying to load your pre-trained model. More than likely, you do not have enough RAM in your computer to hold this model in memory. Consider using a model with a smaller vocabulary or fewer dimensions.", "Plugin Error (Out of Memory)", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


        }





public bool InspectSettings()
        {

            if (string.IsNullOrEmpty(IncomingTextLocation))
            {
                return false;
            }
            else
            {
                return true;
            }
            
        }
        

        public Payload FinishUp(Payload Input)
        {
            Array.Clear(model, 0, model.Length);
            ListOfAllWords.Clear();
            return (Input);
        }




        #region Import/Export Settings
        public void ImportSettings(Dictionary<string, string> SettingsDict)
        {
            SelectedEncoding = SettingsDict["SelectedEncoding"];
            IncomingTextLocation = SettingsDict["IncomingTextLocation"];
            VocabSize = int.Parse(SettingsDict["VocabSize"]);
            VectorSize = int.Parse(SettingsDict["VectorSize"]);
            int WordListLength = int.Parse(SettingsDict["WordListLength"]);

            WordList = new string[WordListLength];

            for (int i = 0; i < WordListLength; i++)
            {
                WordList[i] = SettingsDict["WordList" + i.ToString()];
            }

        }

        public Dictionary<string, string> ExportSettings(bool suppressWarnings)
        {
            Dictionary<string, string> SettingsDict = new Dictionary<string, string>();
            SettingsDict.Add("SelectedEncoding", SelectedEncoding);
            SettingsDict.Add("IncomingTextLocation", IncomingTextLocation);
            SettingsDict.Add("VocabSize", VocabSize.ToString());
            SettingsDict.Add("VectorSize", VectorSize.ToString());

            int WordListLength = 0;
            if (WordList != null) WordListLength = WordList.Length;

            SettingsDict.Add("WordListLength", WordListLength.ToString());

            for (int i = 0; i < WordListLength; i++)
            {
                SettingsDict.Add("WordList" + i.ToString(), WordList[i]);
            }

            return (SettingsDict);
        }
        #endregion


    }
}
