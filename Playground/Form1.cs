using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using Amica.vNext.Objects;
using Amica.Data;
using AutoMapper;

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

            var countries = FromAmica.ToList<Country>(dp.Nazioni);

            //var ent = dp.Anagrafiche.NewAnagraficheRow();
            //ent.RagioneSociale1 = "r1";
            //ent.Id = 9;
            //dp.Anagrafiche.AddAnagraficheRow(ent);

            //var t = (companyDataSet.NazioniDataTable)g.GetChanges(DataRowState.Added);
            //var t = (companyDataSet.NazioniDataTable)dp.Nazioni.GetChanges(DataRowState.Added);
            //var countries = Mapper.Map<List<companyDataSet.NazioniRow>, List<Country>>(t.ToList());
            
            //var a = (companyDataSet.AnagraficheDataTable) dp.Anagrafiche.GetChanges(DataRowState.Added);
            //var country = Mapper.Map<Country>(nr);
            //var c = Mapper.Map(nr, typeof(Country));
        }
    }
}
