using System;
using System.Data.SqlClient;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Linq; // Agrega esta directiva de uso
using System.IO;
using System.IO.Compression;

namespace GuardadordeXML
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Ruta del archivo ZIP
            string zipPath = @"C:\Users\diego\Downloads\FacturaTimbrada.zip";
            string xmlPath = ExtractXMLFromZip(zipPath);

            if (xmlPath == null)
            {
                MessageBox.Show("No se encontró un archivo XML en el ZIP.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Leer el XML y obtener los datos
            XDocument xmlDoc = XDocument.Load(xmlPath);
            XElement comprobanteElement = xmlDoc.Element(XName.Get("Comprobante", "http://www.sat.gob.mx/cfd/4"));
            XElement timbreElement = comprobanteElement.Descendants(XName.Get("TimbreFiscalDigital", "http://www.sat.gob.mx/TimbreFiscalDigital")).FirstOrDefault();
            string uuid = timbreElement.Attribute("UUID").Value;

            // Comprobar si la factura ya existe en la base de datos
            if (FacturaExiste(uuid))
            {
                MessageBox.Show("La factura XML ya ha sido guardada anteriormente dentro de la Base de Datos.");
            }
            else
            {
                GuardarFacturaEnBaseDeDatos(comprobanteElement, timbreElement);
                MessageBox.Show("Factura XML guardada con éxito.");

                // Mover el archivo XML procesado a la carpeta "Facturas-Procesadas"
                string processedXmlFolderPath = @"C:\Users\diego\Downloads\Facturas-Procesadas";
                Directory.CreateDirectory(processedXmlFolderPath); // Crea la carpeta si no existe

                string newFileName = GenerateUniqueFileName(processedXmlFolderPath, "FacturaTimbrada", "xml");
                string processedXmlPath = Path.Combine(processedXmlFolderPath, newFileName);

                File.Move(xmlPath, processedXmlPath);
            }
        }

        // Método para extraer el archivo XML desde el archivo ZIP
        private string ExtractXMLFromZip(string zipPath)
        {
            string tempFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolderPath);

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            string destinationPath = Path.Combine(tempFolderPath, entry.FullName);
                            entry.ExtractToFile(destinationPath);
                            return destinationPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al extraer el archivo XML: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return null;
        }

        // Método para generar un nombre de archivo único
        private string GenerateUniqueFileName(string folderPath, string baseFileName, string extension)
        {
            int fileCount = 1;
            string newFileName;
            do
            {
                newFileName = $"{baseFileName}-[{fileCount}].{extension}";
                fileCount++;
            } while (File.Exists(Path.Combine(folderPath, newFileName)));

            return newFileName;
        }

        private bool FacturaExiste(string uuid)
        {
            string connectionString = "Server=(local); Database=BaseDatosXML; Integrated Security=true;Encrypt=False";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Comprobante WHERE UUID = @UUID", conn);
                cmd.Parameters.AddWithValue("@UUID", uuid);

                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        private void GuardarFacturaEnBaseDeDatos(XElement comprobanteElement, XElement timbreElement)
        {
            string connectionString = "Server=(local); Database=BaseDatosXML; Integrated Security=true;Encrypt=False";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Guardar Comprobante
                SqlCommand cmd = new SqlCommand(
                    "INSERT INTO Comprobante (UUID, Version, Folio, Fecha, FormaPago, SubTotal, Total, Descuento, MetodoPago, Moneda, TipoCambio, TipoDeComprobante, LugarExpedicion, Exportacion, NoCertificado, Certificado, Sello) " +
                    "VALUES (@UUID, @Version, @Folio, @Fecha, @FormaPago, @SubTotal, @Total, @Descuento, @MetodoPago, @Moneda, @TipoCambio, @TipoDeComprobante, @LugarExpedicion, @Exportacion, @NoCertificado, @Certificado, @Sello)", conn);

                cmd.Parameters.AddWithValue("@UUID", timbreElement.Attribute("UUID").Value);
                cmd.Parameters.AddWithValue("@Version", comprobanteElement.Attribute("Version").Value);
                cmd.Parameters.AddWithValue("@Folio", comprobanteElement.Attribute("Folio")?.Value ?? string.Empty);
                cmd.Parameters.AddWithValue("@Fecha", DateTime.Parse(comprobanteElement.Attribute("Fecha").Value));
                cmd.Parameters.AddWithValue("@FormaPago", comprobanteElement.Attribute("FormaPago").Value);
                cmd.Parameters.AddWithValue("@SubTotal", decimal.Parse(comprobanteElement.Attribute("SubTotal").Value));
                cmd.Parameters.AddWithValue("@Total", decimal.Parse(comprobanteElement.Attribute("Total").Value));
                cmd.Parameters.AddWithValue("@Descuento", decimal.Parse(comprobanteElement.Attribute("Descuento")?.Value ?? "0"));
                cmd.Parameters.AddWithValue("@MetodoPago", comprobanteElement.Attribute("MetodoPago").Value);
                cmd.Parameters.AddWithValue("@Moneda", comprobanteElement.Attribute("Moneda").Value);
                cmd.Parameters.AddWithValue("@TipoCambio", decimal.Parse(comprobanteElement.Attribute("TipoCambio")?.Value ?? "0"));
                cmd.Parameters.AddWithValue("@TipoDeComprobante", comprobanteElement.Attribute("TipoDeComprobante").Value);
                cmd.Parameters.AddWithValue("@LugarExpedicion", comprobanteElement.Attribute("LugarExpedicion").Value);
                cmd.Parameters.AddWithValue("@Exportacion", comprobanteElement.Attribute("Exportacion")?.Value ?? string.Empty);
                cmd.Parameters.AddWithValue("@NoCertificado", comprobanteElement.Attribute("NoCertificado").Value);
                cmd.Parameters.AddWithValue("@Certificado", comprobanteElement.Attribute("Certificado").Value);
                cmd.Parameters.AddWithValue("@Sello", comprobanteElement.Attribute("Sello").Value);

                cmd.ExecuteNonQuery();

                // Guardar Emisor
                XElement emisorElement = comprobanteElement.Element(XName.Get("Emisor", "http://www.sat.gob.mx/cfd/4"));
                SqlCommand cmdEmisor = new SqlCommand(
                    "INSERT INTO Emisor (UUID, Nombre, Rfc, RegimenFiscal) " +
                    "VALUES (@UUID, @Nombre, @Rfc, @RegimenFiscal)", conn);
                cmdEmisor.Parameters.AddWithValue("@UUID", timbreElement.Attribute("UUID").Value);
                cmdEmisor.Parameters.AddWithValue("@Nombre", emisorElement.Attribute("Nombre").Value);
                cmdEmisor.Parameters.AddWithValue("@Rfc", emisorElement.Attribute("Rfc").Value);
                cmdEmisor.Parameters.AddWithValue("@RegimenFiscal", emisorElement.Attribute("RegimenFiscal").Value);

                cmdEmisor.ExecuteNonQuery();

                // Guardar Receptor
                XElement receptorElement = comprobanteElement.Element(XName.Get("Receptor", "http://www.sat.gob.mx/cfd/4"));
                SqlCommand cmdReceptor = new SqlCommand(
                    "INSERT INTO Receptor (UUID, Nombre, Rfc, UsoCFDI, DomicilioFiscalReceptor, RegimenFiscalReceptor) " +
                    "VALUES (@UUID, @Nombre, @Rfc, @UsoCFDI, @DomicilioFiscalReceptor, @RegimenFiscalReceptor)", conn);
                cmdReceptor.Parameters.AddWithValue("@UUID", timbreElement.Attribute("UUID").Value);
                cmdReceptor.Parameters.AddWithValue("@Nombre", receptorElement.Attribute("Nombre").Value);
                cmdReceptor.Parameters.AddWithValue("@Rfc", receptorElement.Attribute("Rfc").Value);
                cmdReceptor.Parameters.AddWithValue("@UsoCFDI", receptorElement.Attribute("UsoCFDI").Value);
                cmdReceptor.Parameters.AddWithValue("@DomicilioFiscalReceptor", receptorElement.Attribute("DomicilioFiscalReceptor").Value);
                cmdReceptor.Parameters.AddWithValue("@RegimenFiscalReceptor", receptorElement.Attribute("RegimenFiscalReceptor").Value);

                cmdReceptor.ExecuteNonQuery();

                // Guardar Conceptos
                var conceptos = comprobanteElement.Element(XName.Get("Conceptos", "http://www.sat.gob.mx/cfd/4")).Elements(XName.Get("Concepto", "http://www.sat.gob.mx/cfd/4"));
                foreach (var concepto in conceptos)
                {
                    SqlCommand cmdConcepto = new SqlCommand(
                        "INSERT INTO Concepto (UUID, ClaveProdServ, Cantidad, ClaveUnidad, Unidad, NoIdentificacion, Descripcion, ValorUnitario, Importe, Descuento, ObjetoImp) " +
                        "VALUES (@UUID, @ClaveProdServ, @Cantidad, @ClaveUnidad, @Unidad, @NoIdentificacion, @Descripcion, @ValorUnitario, @Importe, @Descuento, @ObjetoImp); SELECT SCOPE_IDENTITY();", conn);
                    cmdConcepto.Parameters.AddWithValue("@UUID", timbreElement.Attribute("UUID").Value);
                    cmdConcepto.Parameters.AddWithValue("@ClaveProdServ", concepto.Attribute("ClaveProdServ").Value);
                    cmdConcepto.Parameters.AddWithValue("@Cantidad", decimal.Parse(concepto.Attribute("Cantidad").Value));
                    cmdConcepto.Parameters.AddWithValue("@ClaveUnidad", concepto.Attribute("ClaveUnidad").Value);
                    cmdConcepto.Parameters.AddWithValue("@Unidad", concepto.Attribute("Unidad").Value);
                    cmdConcepto.Parameters.AddWithValue("@NoIdentificacion", concepto.Attribute("NoIdentificacion")?.Value ?? string.Empty);
                    cmdConcepto.Parameters.AddWithValue("@Descripcion", concepto.Attribute("Descripcion").Value);
                    cmdConcepto.Parameters.AddWithValue("@ValorUnitario", decimal.Parse(concepto.Attribute("ValorUnitario").Value));
                    cmdConcepto.Parameters.AddWithValue("@Importe", decimal.Parse(concepto.Attribute("Importe").Value));
                    cmdConcepto.Parameters.AddWithValue("@Descuento", decimal.Parse(concepto.Attribute("Descuento")?.Value ?? "0"));
                    cmdConcepto.Parameters.AddWithValue("@ObjetoImp", concepto.Attribute("ObjetoImp").Value);

                    // Insertar el concepto y obtener el ConceptoID insertado
                    int conceptoID = Convert.ToInt32(cmdConcepto.ExecuteScalar());

                    // Ahora, para los impuestos del concepto:
                    var impuestosConcepto = concepto.Element(XName.Get("Impuestos", "http://www.sat.gob.mx/cfd/4"));
                    if (impuestosConcepto != null)
                    {
                        var impuestos = impuestosConcepto.Elements(XName.Get("Traslados", "http://www.sat.gob.mx/cfd/4")).Elements(XName.Get("Traslado", "http://www.sat.gob.mx/cfd/4"));
                        foreach (var impuesto in impuestos)
                        {
                            SqlCommand cmdImpuesto = new SqlCommand(
                                "INSERT INTO Impuesto (ConceptoID, Impuesto, TipoFactor, TasaOCuota, Importe, Base) " +
                                "VALUES (@ConceptoID, @Impuesto, @TipoFactor, @TasaOCuota, @Importe, @Base)", conn);
                            cmdImpuesto.Parameters.AddWithValue("@ConceptoID", conceptoID);
                            cmdImpuesto.Parameters.AddWithValue("@Impuesto", impuesto.Attribute("Impuesto").Value);
                            cmdImpuesto.Parameters.AddWithValue("@TipoFactor", impuesto.Attribute("TipoFactor").Value);
                            cmdImpuesto.Parameters.AddWithValue("@TasaOCuota", decimal.Parse(impuesto.Attribute("TasaOCuota").Value));
                            cmdImpuesto.Parameters.AddWithValue("@Importe", decimal.Parse(impuesto.Attribute("Importe").Value));
                            cmdImpuesto.Parameters.AddWithValue("@Base", decimal.Parse(impuesto.Attribute("Base").Value));

                            cmdImpuesto.ExecuteNonQuery();
                        }
                    }
                }

                // Guardar Timbre Fiscal Digital
                SqlCommand cmdTimbre = new SqlCommand(
                    "INSERT INTO TimbreFiscalDigital (UUID, Version, FechaTimbrado, SelloCFD, NoCertificadoSAT, SelloSAT, RfcProvCertif) " +
                    "VALUES (@UUID, @Version, @FechaTimbrado, @SelloCFD, @NoCertificadoSAT, @SelloSAT, @RfcProvCertif)", conn);
                cmdTimbre.Parameters.AddWithValue("@UUID", timbreElement.Attribute("UUID").Value);
                cmdTimbre.Parameters.AddWithValue("@Version", timbreElement.Attribute("Version").Value);
                cmdTimbre.Parameters.AddWithValue("@FechaTimbrado", DateTime.Parse(timbreElement.Attribute("FechaTimbrado").Value));
                cmdTimbre.Parameters.AddWithValue("@SelloCFD", timbreElement.Attribute("SelloCFD").Value);
                cmdTimbre.Parameters.AddWithValue("@NoCertificadoSAT", timbreElement.Attribute("NoCertificadoSAT").Value);
                cmdTimbre.Parameters.AddWithValue("@SelloSAT", timbreElement.Attribute("SelloSAT").Value);
                cmdTimbre.Parameters.AddWithValue("@RfcProvCertif", timbreElement.Attribute("RfcProvCertif").Value);

                cmdTimbre.ExecuteNonQuery();
            }
        }
    }
}
