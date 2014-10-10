using System;
using System.Windows.Forms;
using Amica.vNext.Objects;
using Amica.Data;

namespace Playground
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {


            //Mapper.Configuration.AddProfile<NazioniProfile>();

            var dp = new companyDataSet();


            var nr = dp.Nazioni.NewNazioniRow();
            nr.Nome = "nome";
            nr.Id = 99;
            dp.Nazioni.AddNazioniRow(nr);

            //var a = dp.AreeGeografiche.NewAreeGeograficheRow();
            //nr.Nome = "nome";
            //nr.Id = 99;
            //dp.AreeGeografiche.AddAreeGeograficheRow(a);

            var countries = FromAmica.ToList<Country>(dp.Nazioni);
            var country = FromAmica.To<Country>(nr);

        }
    }
}
