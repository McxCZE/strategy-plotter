using System.Globalization;
//double Strategy_Gamma::IntegrationTable::mainFunction(double x) const {
//	switch (fn) {
//	case halfhalf: return std::exp(-(std::pow(x, z)) - 0.5 * std::log(x));
//	case keepvalue: return std::exp(-std::pow(x, z)) / x;
//	case exponencial: return std::exp(-std::pow(x, z));
//	case gauss: return std::exp(-pow2(x) - std::pow(x, z));
//	case invsqrtsinh: return 1.0 / std::sqrt(std::sinh(std::pow(x * 1.2, z)));
//    default : return 0;
//}
//}

static class ExtensionClass
{
    public static string Ts(this double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static StreamWriter Add(this StreamWriter writer, string value, bool last = false)
    {
        writer.Write(value);
        if (last)
        {
            writer.WriteLine();
        }
        else
        {
            writer.Write(',');
        }
        return writer;
    }

    public static StreamWriter Add(this StreamWriter writer, double value, bool last = false)
    {
        return writer.Add(value.Ts(), last);
    }
}