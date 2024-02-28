using ExtremeExtensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace xFences
{
    public class Settings
    {
        public List<SpaceFormSettings> SpaceFormSettings = new List<SpaceFormSettings>();
        public static Settings Load()
        {
            var filename = Path.Combine(Directory.GetCurrentDirectory(), "Settings.xml");
            return Serializer.LoadXml<Settings>(filename);
        }

        public void Save()
        {
            var filename = Path.Combine(Directory.GetCurrentDirectory(), "Settings.xml");
            Serializer.SaveXml(filename, this);
        }
    }

    public class SpaceFormSettings
    {
        public SpaceFormSettings() { }
        public SpaceFormSettings(SpaceForm s) 
        { 
            Name = s.Text; rect = new Rectangle(s.Location,s.Size); Folder = s.SpaceFolder; 
            ForeColor = s.ForeColor;
            BackColor = s.BackColor;
        }
        public SpaceForm GetSpaceForm() 
        { 
            var spaceForm = new SpaceForm();
            spaceForm.Name = Name;
            spaceForm.Text = Name;
            spaceForm.BackColor = BackColor;
            spaceForm.ForeColor = ForeColor;
            spaceForm.SpaceFolder = Folder;
            spaceForm.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            spaceForm.Size = rect.Size;
            spaceForm.Location = rect.Location;
            return spaceForm;
        }

        public string Name;
        public Rectangle rect;
        public string Folder;
        public string foreColor = ColorTranslator.ToHtml(Color.Black);
        public string backColor = ColorTranslator.ToHtml(Color.White);

        [XmlIgnore]
        public Color ForeColor { get => ColorTranslator.FromHtml(foreColor); set => foreColor = ColorTranslator.ToHtml(value); }
        [XmlIgnore]
        public Color BackColor { get => ColorTranslator.FromHtml(backColor); set => backColor = ColorTranslator.ToHtml(value); }
    }
}
