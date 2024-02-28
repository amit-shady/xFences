﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace xFences
{
    public partial class SpaceName : Form
    {
        public SpaceName()
        {
            InitializeComponent();
        }

        public string DialogName { get { return textBox1.Text; } set { textBox1.Text = value; } }
        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
