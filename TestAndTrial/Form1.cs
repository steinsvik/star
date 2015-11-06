using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ebs.Star.Core;

namespace TestAndTrial
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string s;
            
            s = "132412";
            //string t = s.

            textBox1.Text = "1122".ToHexBytesString();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
