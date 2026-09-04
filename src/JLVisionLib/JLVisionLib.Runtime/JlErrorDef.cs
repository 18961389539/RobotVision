namespace JLVisionLib;

	/// <summary>原生库返回码与错误码常量集合（Jl_MSG_* 状态码与 Jl_ERR_* 错误码）。</summary>
public class JlErrorDef
{
	/// <summary>Normal return value </summary>
	public const int Jl_MSG_OK = 2;

	/// <summary>true </summary>
	public const int Jl_MSG_TRUE = 2;

	/// <summary>false </summary>
	public const int Jl_MSG_FALSE = 3;

	/// <summary>Stop processing </summary>
	public const int Jl_MSG_VOID = 4;

	/// <summary>Call failed </summary>
	public const int Jl_MSG_FAIL = 5;

	/// <summary>for internal use </summary>
	public const int Jl_ERR_BREAK = 20;

	/// <summary>operator was canceled for dev engine </summary>
	public const int Jl_ERR_ENGINE_CANCEL = 21;

	/// <summary>operator was generally cancelled </summary>
	public const int Jl_ERR_CANCEL = 22;

	/// <summary>for internal use </summary>
	public const int Jl_ERR_TIMEOUT_BREAK = 23;

	/// <summary>Wrong type of control parameter: 1 </summary>
	public const int Jl_ERR_WIPT1 = 1201;

	/// <summary>Wrong type of control parameter: 2 </summary>
	public const int Jl_ERR_WIPT2 = 1202;

	/// <summary>Wrong type of control parameter: 3 </summary>
	public const int Jl_ERR_WIPT3 = 1203;

	/// <summary>Wrong type of control parameter: 4 </summary>
	public const int Jl_ERR_WIPT4 = 1204;

	/// <summary>Wrong type of control parameter: 5 </summary>
	public const int Jl_ERR_WIPT5 = 1205;

	/// <summary>Wrong type of control parameter: 6 </summary>
	public const int Jl_ERR_WIPT6 = 1206;

	/// <summary>Wrong type of control parameter: 7 </summary>
	public const int Jl_ERR_WIPT7 = 1207;

	/// <summary>Wrong type of control parameter: 8 </summary>
	public const int Jl_ERR_WIPT8 = 1208;

	/// <summary>Wrong type of control parameter: 9 </summary>
	public const int Jl_ERR_WIPT9 = 1209;

	/// <summary>Wrong type of control parameter: 10 </summary>
	public const int Jl_ERR_WIPT10 = 1210;

	/// <summary>Wrong type of control parameter: 11 </summary>
	public const int Jl_ERR_WIPT11 = 1211;

	/// <summary>Wrong type of control parameter: 12 </summary>
	public const int Jl_ERR_WIPT12 = 1212;

	/// <summary>Wrong type of control parameter: 13 </summary>
	public const int Jl_ERR_WIPT13 = 1213;

	/// <summary>Wrong type of control parameter: 14 </summary>
	public const int Jl_ERR_WIPT14 = 1214;

	/// <summary>Wrong type of control parameter: 15 </summary>
	public const int Jl_ERR_WIPT15 = 1215;

	/// <summary>Wrong type of control parameter: 16 </summary>
	public const int Jl_ERR_WIPT16 = 1216;

	/// <summary>Wrong type of control parameter: 17 </summary>
	public const int Jl_ERR_WIPT17 = 1217;

	/// <summary>Wrong type of control parameter: 18 </summary>
	public const int Jl_ERR_WIPT18 = 1218;

	/// <summary>Wrong type of control parameter: 19 </summary>
	public const int Jl_ERR_WIPT19 = 1219;

	/// <summary>Wrong type of control parameter: 20 </summary>
	public const int Jl_ERR_WIPT20 = 1220;

	/// <summary>Wrong value of control parameter: 1 </summary>
	public const int Jl_ERR_WIPV1 = 1301;

	/// <summary>Wrong value of control parameter: 2 </summary>
	public const int Jl_ERR_WIPV2 = 1302;

	/// <summary>Wrong value of control parameter: 3 </summary>
	public const int Jl_ERR_WIPV3 = 1303;

	/// <summary>Wrong value of control parameter: 4 </summary>
	public const int Jl_ERR_WIPV4 = 1304;

	/// <summary>Wrong value of control parameter: 5 </summary>
	public const int Jl_ERR_WIPV5 = 1305;

	/// <summary>Wrong value of control parameter: 6 </summary>
	public const int Jl_ERR_WIPV6 = 1306;

	/// <summary>Wrong value of control parameter: 7 </summary>
	public const int Jl_ERR_WIPV7 = 1307;

	/// <summary>Wrong value of control parameter: 8 </summary>
	public const int Jl_ERR_WIPV8 = 1308;

	/// <summary>Wrong value of control parameter: 9 </summary>
	public const int Jl_ERR_WIPV9 = 1309;

	/// <summary>Wrong value of control parameter: 10 </summary>
	public const int Jl_ERR_WIPV10 = 1310;

	/// <summary>Wrong value of control parameter: 11 </summary>
	public const int Jl_ERR_WIPV11 = 1311;

	/// <summary>Wrong value of control parameter: 12 </summary>
	public const int Jl_ERR_WIPV12 = 1312;

	/// <summary>Wrong value of control parameter: 13 </summary>
	public const int Jl_ERR_WIPV13 = 1313;

	/// <summary>Wrong value of control parameter: 14 </summary>
	public const int Jl_ERR_WIPV14 = 1314;

	/// <summary>Wrong value of control parameter: 15 </summary>
	public const int Jl_ERR_WIPV15 = 1315;

	/// <summary>Wrong value of control parameter: 16 </summary>
	public const int Jl_ERR_WIPV16 = 1316;

	/// <summary>Wrong value of control parameter: 17 </summary>
	public const int Jl_ERR_WIPV17 = 1317;

	/// <summary>Wrong value of control parameter: 18 </summary>
	public const int Jl_ERR_WIPV18 = 1318;

	/// <summary>Wrong value of control parameter: 19 </summary>
	public const int Jl_ERR_WIPV19 = 1319;

	/// <summary>Wrong value of control parameter: 20 </summary>
	public const int Jl_ERR_WIPV20 = 1320;

	/// <summary>Wrong value of component </summary>
	public const int Jl_ERR_WCOMP = 1350;

	/// <summary>Wrong value of gray value component </summary>
	public const int Jl_ERR_WGCOMP = 1351;

	/// <summary>Wrong number of values of ctrl.par.: 1 </summary>
	public const int Jl_ERR_WIPN1 = 1401;

	/// <summary>Wrong number of values of ctrl.par.: 2 </summary>
	public const int Jl_ERR_WIPN2 = 1402;

	/// <summary>Wrong number of values of ctrl.par.: 3 </summary>
	public const int Jl_ERR_WIPN3 = 1403;

	/// <summary>Wrong number of values of ctrl.par.: 4 </summary>
	public const int Jl_ERR_WIPN4 = 1404;

	/// <summary>Wrong number of values of ctrl.par.: 5 </summary>
	public const int Jl_ERR_WIPN5 = 1405;

	/// <summary>Wrong number of values of ctrl.par.: 6 </summary>
	public const int Jl_ERR_WIPN6 = 1406;

	/// <summary>Wrong number of values of ctrl.par.: 7 </summary>
	public const int Jl_ERR_WIPN7 = 1407;

	/// <summary>Wrong number of values of ctrl.par.: 8 </summary>
	public const int Jl_ERR_WIPN8 = 1408;

	/// <summary>Wrong number of values of ctrl.par.: 9 </summary>
	public const int Jl_ERR_WIPN9 = 1409;

	/// <summary>Wrong number of values of ctrl.par.: 10 </summary>
	public const int Jl_ERR_WIPN10 = 1410;

	/// <summary>Wrong number of values of ctrl.par.: 11 </summary>
	public const int Jl_ERR_WIPN11 = 1411;

	/// <summary>Wrong number of values of ctrl.par.: 12 </summary>
	public const int Jl_ERR_WIPN12 = 1412;

	/// <summary>Wrong number of values of ctrl.par.: 13 </summary>
	public const int Jl_ERR_WIPN13 = 1413;

	/// <summary>Wrong number of values of ctrl.par.: 14 </summary>
	public const int Jl_ERR_WIPN14 = 1414;

	/// <summary>Wrong number of values of ctrl.par.: 15 </summary>
	public const int Jl_ERR_WIPN15 = 1415;

	/// <summary>Wrong number of values of ctrl.par.: 16 </summary>
	public const int Jl_ERR_WIPN16 = 1416;

	/// <summary>Wrong number of values of ctrl.par.: 17 </summary>
	public const int Jl_ERR_WIPN17 = 1417;

	/// <summary>Wrong number of values of ctrl.par.: 18 </summary>
	public const int Jl_ERR_WIPN18 = 1418;

	/// <summary>Wrong number of values of ctrl.par.: 19 </summary>
	public const int Jl_ERR_WIPN19 = 1419;

	/// <summary>Wrong number of values of ctrl.par.: 20 </summary>
	public const int Jl_ERR_WIPN20 = 1420;

	/// <summary>Number of input objects too big </summary>
	public const int Jl_ERR_IONTB = 1500;

	/// <summary>Wrong number of values of object par.: 1 </summary>
	public const int Jl_ERR_WION1 = 1501;

	/// <summary>Wrong number of values of object par.: 2 </summary>
	public const int Jl_ERR_WION2 = 1502;

	/// <summary>Wrong number of values of object par.: 3 </summary>
	public const int Jl_ERR_WION3 = 1503;

	/// <summary>Wrong number of values of object par.: 4 </summary>
	public const int Jl_ERR_WION4 = 1504;

	/// <summary>Wrong number of values of object par.: 5 </summary>
	public const int Jl_ERR_WION5 = 1505;

	/// <summary>Wrong number of values of object par.: 6 </summary>
	public const int Jl_ERR_WION6 = 1506;

	/// <summary>Wrong number of values of object par.: 7 </summary>
	public const int Jl_ERR_WION7 = 1507;

	/// <summary>Wrong number of values of object par.: 8 </summary>
	public const int Jl_ERR_WION8 = 1508;

	/// <summary>Wrong number of values of object par.: 9 </summary>
	public const int Jl_ERR_WION9 = 1509;

	/// <summary>Number of output objects too big </summary>
	public const int Jl_ERR_OONTB = 1510;

	/// <summary>Wrong specification of parameter (error in file: xxx.def) </summary>
	public const int Jl_ERR_WNP = 2000;

	/// <summary>Initialize Vision: reset_obj_db(Width,Height,Components) </summary>
	public const int Jl_ERR_HONI = 2001;

	/// <summary>Used number of symbolic object names too big </summary>
	public const int Jl_ERR_WRKNN = 2002;

	/// <summary>No license found </summary>
	public const int Jl_ERR_LIC_NO_LICENSE = 2003;

	/// <summary>License type not implemented in this version of Vision </summary>
	public const int Jl_ERR_LIC_NOT_IMPLEMENTED = 2004;

	/// <summary>No modules in license (no VENDOR_STRING) </summary>
	public const int Jl_ERR_LIC_NO_MODULES = 2005;

	/// <summary>No license for this operator </summary>
	public const int Jl_ERR_LIC_NO_LIC_OPER = 2006;

	/// <summary>Vendor keys do not support this platform </summary>
	public const int Jl_ERR_LIC_BADPLATFORM = 2008;

	/// <summary>Bad vendor keys </summary>
	public const int Jl_ERR_LIC_BADVENDORKEY = 2009;

	/// <summary>System clock has been set back </summary>
	public const int Jl_ERR_LIC_BADSYSDATE = 2021;

	/// <summary>Version argument is invalid floating point format </summary>
	public const int Jl_ERR_LIC_BAD_VERSION = 2022;

	/// <summary>Cannot establish a connection with a license server </summary>
	public const int Jl_ERR_LIC_CANTCONNECT = 2024;

	/// <summary>Session limit exceeded </summary>
	public const int Jl_ERR_LIC_MAXSESSIONS = 2028;

	/// <summary>All licenses in use </summary>
	public const int Jl_ERR_LIC_MAXUSERS = 2029;

	/// <summary>No license server specified for counted license </summary>
	public const int Jl_ERR_LIC_NO_SERVER_IN_FILE = 2030;

	/// <summary>Can not find feature in the license file </summary>
	public const int Jl_ERR_LIC_NOFEATURE = 2031;

	/// <summary>License file does not support a version this new </summary>
	public const int Jl_ERR_LIC_OLDVER = 2033;

	/// <summary>This platform not authorized by license - running on platform not included in PLATFORMS list </summary>
	public const int Jl_ERR_LIC_PLATNOTLIC = 2034;

	/// <summary>License server busy </summary>
	public const int Jl_ERR_LIC_SERVBUSY = 2035;

	/// <summary>Could not find license.dat </summary>
	public const int Jl_ERR_LIC_NOCONFFILE = 2036;

	/// <summary>Invalid license file syntax </summary>
	public const int Jl_ERR_LIC_BADFILE = 2037;

	/// <summary>Cannot connect to a license server </summary>
	public const int Jl_ERR_LIC_NOSERVER = 2038;

	/// <summary>Invalid host </summary>
	public const int Jl_ERR_LIC_NOTTHISHOST = 2041;

	/// <summary>Feature has expired </summary>
	public const int Jl_ERR_LIC_LONGGONE = 2042;

	/// <summary>Invalid date format in license file </summary>
	public const int Jl_ERR_LIC_BADDATE = 2043;

	/// <summary>Invalid returned data from license server </summary>
	public const int Jl_ERR_LIC_BADCOMM = 2044;

	/// <summary>Cannot find SERVER hostname in network database </summary>
	public const int Jl_ERR_LIC_BADHOST = 2045;

	/// <summary>Cannot write data to license server </summary>
	public const int Jl_ERR_LIC_CANTWRITE = 2047;

	/// <summary>License server does not support this version of this feature </summary>
	public const int Jl_ERR_LIC_SERVLONGGONE = 2051;

	/// <summary>Request for more licenses than this feature supports </summary>
	public const int Jl_ERR_LIC_TOOMANY = 2052;

	/// <summary>Cannot find ethernet device </summary>
	public const int Jl_ERR_LIC_CANTFINDETHER = 2055;

	/// <summary>Cannot read license file </summary>
	public const int Jl_ERR_LIC_NOREADLIC = 2056;

	/// <summary>Date too late for binary format </summary>
	public const int Jl_ERR_LIC_DATE_TOOBIG = 2067;

	/// <summary>Server did not respond to message </summary>
	public const int Jl_ERR_LIC_NOSERVRESP = 2069;

	/// <summary>setsockopt() failed </summary>
	public const int Jl_ERR_LIC_SETSOCKFAIL = 2075;

	/// <summary>Message checksum failure </summary>
	public const int Jl_ERR_LIC_BADCHECKSUM = 2076;

	/// <summary>Internal licensing error </summary>
	public const int Jl_ERR_LIC_INTERNAL_ERROR = 2082;

	/// <summary>Server doesn't support this request </summary>
	public const int Jl_ERR_LIC_NOSERVCAP = 2087;

	/// <summary>This feature is available in a different license pool </summary>
	public const int Jl_ERR_LIC_POOL = 2091;

	/// <summary>Dongle not attached, or can't read dongle </summary>
	public const int Jl_ERR_LIC_NODONGLE = 2300;

	/// <summary>Missing dongle driver </summary>
	public const int Jl_ERR_LIC_NODONGLEDRIVER = 2301;

	/// <summary>Timeout </summary>
	public const int Jl_ERR_LIC_TIMEOUT = 2318;

	/// <summary>Invalid license server certificate </summary>
	public const int Jl_ERR_LIC_INVALID_CERTIFICATE = 2321;

	/// <summary>Invalid license server SSL/TLS certificate </summary>
	public const int Jl_ERR_LIC_INVALID_TLS_CERTIFICATE = 2335;

	/// <summary>Invalid activation request received </summary>
	public const int Jl_ERR_LIC_BAD_ACTREQ = 2339;

	/// <summary>Specified operation is not allowed </summary>
	public const int Jl_ERR_LIC_NOT_ALLOWED = 2345;

	/// <summary>Activation error </summary>
	public const int Jl_ERR_LIC_ACTIVATION = 2348;

	/// <summary>No CodeMeter Runtime installed </summary>
	public const int Jl_ERR_LIC_NO_CM_RUNTIME = 2379;

	/// <summary>Installed CodeMeter Runtime is too old </summary>
	public const int Jl_ERR_LIC_CM_RUNTIME_TOO_OLD = 2380;

	/// <summary>License is for wrong Vision edition </summary>
	public const int Jl_ERR_LIC_WRONG_EDITION = 2381;

	/// <summary>License contains unknown FLAGS </summary>
	public const int Jl_ERR_LIC_UNKNOWN_FLAGS = 2382;

	/// <summary>Vision preview version expired </summary>
	public const int Jl_ERR_LIC_PREVIEW_EXPIRED = 2383;

	/// <summary>License does not support a Vision version this old </summary>
	public const int Jl_ERR_LIC_NEWVER = 2384;

	/// <summary>Error codes concerning the Vision core, 2100..2199 </summary>
	public const int Jl_ERR_LIC_RANGE1_BEGIN = 2003;

	/// <summary>Wrong index for output object parameter </summary>
	public const int Jl_ERR_WOOPI = 2100;

	/// <summary>Wrong index for input object parameter</summary>
	public const int Jl_ERR_WIOPI = 2101;

	/// <summary>Wrong index for image object </summary>
	public const int Jl_ERR_WOI = 2102;

	/// <summary>Wrong number region/image component </summary>
	public const int Jl_ERR_WRCN = 2103;

	/// <summary>Wrong relation name </summary>
	public const int Jl_ERR_WRRN = 2104;

	/// <summary>Access to undefined gray value component</summary>
	public const int Jl_ERR_AUDI = 2105;

	/// <summary>Wrong image width </summary>
	public const int Jl_ERR_WIWI = 2106;

	/// <summary>Wrong image height </summary>
	public const int Jl_ERR_WIHE = 2107;

	/// <summary>Undefined gray value component </summary>
	public const int Jl_ERR_ICUNDEF = 2108;

	/// <summary>Inconsistent data of data base (typing) </summary>
	public const int Jl_ERR_IDBD = 2200;

	/// <summary>Wrong index for input control parameter </summary>
	public const int Jl_ERR_WICPI = 2201;

	/// <summary>Data of data base not defined (internal error) </summary>
	public const int Jl_ERR_DBDU = 2202;

	/// <summary>legacy: Number of operators too big </summary>
	public const int Jl_ERR_PNTL = 2203;

	/// <summary>User extension not properly installed </summary>
	public const int Jl_ERR_UEXTNI = 2205;

	/// <summary>legacy: Number of packages too large </summary>
	public const int Jl_ERR_NPTL = 2206;

	/// <summary>No such package installed </summary>
	public const int Jl_ERR_NSP = 2207;

	/// <summary>incompatible Vision versions </summary>
	public const int Jl_ERR_ICHV = 2211;

	/// <summary>incompatible operator interface </summary>
	public const int Jl_ERR_ICOI = 2212;

	/// <summary>wrong extension package id </summary>
	public const int Jl_ERR_XPKG_WXID = 2220;

	/// <summary>wrong operator id </summary>
	public const int Jl_ERR_XPKG_WOID = 2221;

	/// <summary>wrong operator information id </summary>
	public const int Jl_ERR_XPKG_WOIID = 2222;

	/// <summary>Wrong Hctuple array type </summary>
	public const int Jl_ERR_CTPL_WTYP = 2400;

	/// <summary>Wrong Hcpar type </summary>
	public const int Jl_ERR_CPAR_WTYP = 2401;

	/// <summary>Wrong Hctuple index </summary>
	public const int Jl_ERR_CTPL_WIDX = 2402;

	/// <summary>Wrong version of file </summary>
	public const int Jl_ERR_WFV = 2403;

	/// <summary>Wrong handle type </summary>
	public const int Jl_ERR_WRONG_HANDLE_TYPE = 2404;

	/// <summary>wrong vector type </summary>
	public const int Jl_ERR_WVTYP = 2410;

	/// <summary>wrong vector dimension </summary>
	public const int Jl_ERR_WVDIM = 2411;

	/// <summary>Wrong (unknown) Vision handle </summary>
	public const int Jl_ERR_WHDL = 2450;

	/// <summary>Wrong Vision id, no data available </summary>
	public const int Jl_ERR_WID = 2451;

	/// <summary>Vision id out of range </summary>
	public const int Jl_ERR_IDOOR = 2452;

	/// <summary>Handle is NULL </summary>
	public const int Jl_ERR_HANDLE_NULL = 2453;

	/// <summary>Handle was cleared </summary>
	public const int Jl_ERR_HANDLE_CLEARED = 2454;

	/// <summary>Handle type does not serialize </summary>
	public const int Jl_ERR_HANDLE_NOSER = 2455;

	/// <summary>Reference cycles of handles found </summary>
	public const int Jl_ERR_HANDLE_CYCLES = 2456;

	/// <summary>Type mismatch: Control expected, found iconic </summary>
	public const int Jl_ERR_WT_CTRL_EXPECTED = 2460;

	/// <summary>Type mismatc: Iconic expected, control found </summary>
	public const int Jl_ERR_WT_ICONIC_EXPECTED = 2461;

	/// <summary>extension api Init function of an extension * that was build with xpi was not * called </summary>
	public const int Jl_ERR_XPI_INIT_NOT_CALLED = 2500;

	/// <summary>native library didn't find the init function * of the extension it is connecting to * -&gt; old extension without extension api or * the function export failed </summary>
	public const int Jl_ERR_XPI_NO_INIT_FOUND = 2501;

	/// <summary>Unresolved function in extension api </summary>
	public const int Jl_ERR_XPI_UNRES = 2502;

	/// <summary>Vision extension requires a Vision * version that is newer than the * connected native library </summary>
	public const int Jl_ERR_XPI_LIB_TOO_OLD = 2503;

	/// <summary>the (major) version of the extension api * which is used by the connecting * extension is too small for native library </summary>
	public const int Jl_ERR_XPI_XPI_TOO_OLD = 2504;

	/// <summary>the major version of the extension api * which is used by the native library is too * small </summary>
	public const int Jl_ERR_XPI_MAJOR_TOO_SMALL = 2505;

	/// <summary>the minor version of the extension api * which is used by the native library is too * small </summary>
	public const int Jl_ERR_XPI_MINOR_TOO_SMALL = 2506;

	/// <summary>Wrong major version in symbol struct * (internal: should not happen) </summary>
	public const int Jl_ERR_XPI_INT_WRONG_MAJOR = 2507;

	/// <summary>JlLib version could not be detected </summary>
	public const int Jl_ERR_XPI_UNKNOWN_LIB_VER = 2508;

	/// <summary>Wrong hardware information file format </summary>
	public const int Jl_ERR_HW_WFF = 2800;

	/// <summary>Wrong hardware information file version </summary>
	public const int Jl_ERR_HW_WFV = 2801;

	/// <summary>Error while reading the hardware knowledge</summary>
	public const int Jl_ERR_HW_RF = 2802;

	/// <summary>Error while writing the hardware knowledge</summary>
	public const int Jl_ERR_HW_WF = 2803;

	/// <summary>Tag not found </summary>
	public const int Jl_ERR_HW_TF = 2804;

	/// <summary>No CPU Info </summary>
	public const int Jl_ERR_HW_CPU = 2805;

	/// <summary>No AOP Info </summary>
	public const int Jl_ERR_HW_AOP = 2806;

	/// <summary>No AOP Info for this Vision variant </summary>
	public const int Jl_ERR_HW_HVAR = 2807;

	/// <summary>No AOP Info for this Vision architecture </summary>
	public const int Jl_ERR_HW_HARCH = 2808;

	/// <summary>No AOP Info for specified Operator found </summary>
	public const int Jl_ERR_HW_HOP = 2809;

	/// <summary>undefined AOP model </summary>
	public const int Jl_ERR_HW_WAOPM = 2810;

	/// <summary>wrong tag derivate </summary>
	public const int Jl_ERR_HW_WTD = 2811;

	/// <summary>internal error </summary>
	public const int Jl_ERR_HW_IE = 2812;

	/// <summary>hw check was canceled </summary>
	public const int Jl_ERR_HW_CANCEL = 2813;

	/// <summary>Wrong access to global variable </summary>
	public const int Jl_ERR_GV_WA = 2830;

	/// <summary>Used global variable does not exist </summary>
	public const int Jl_ERR_GV_NC = 2831;

	/// <summary>Used global variable not accessible via GLOBAL_ID </summary>
	public const int Jl_ERR_GV_NG = 2832;

	/// <summary>Vision server to terminate is still working on a job </summary>
	public const int Jl_ERR_HM_NT = 2835;

	/// <summary>No such Vision software agent </summary>
	public const int Jl_ERR_HM_NA = 2837;

	/// <summary>Hardware check for parallelization not possible on a single-processor machine </summary>
	public const int Jl_ERR_AG_CN = 2838;

	/// <summary>(Seq.) Vision does not support parallel hardware check (use Parallel Vision instead) </summary>
	public const int Jl_ERR_AG_NC = 2839;

	/// <summary>Initialization of agent failed </summary>
	public const int Jl_ERR_AG_IN = 2840;

	/// <summary>Termination of agent failed </summary>
	public const int Jl_ERR_AG_NT = 2841;

	/// <summary>Inconsistent hardware description file </summary>
	public const int Jl_ERR_AG_HW = 2842;

	/// <summary>Inconsistent agent information file </summary>
	public const int Jl_ERR_AG_II = 2843;

	/// <summary>Inconsistent agent knowledge file </summary>
	public const int Jl_ERR_AG_IK = 2844;

	/// <summary>The file with the parallelization information does not match to the currently Vision version/revision </summary>
	public const int Jl_ERR_AG_WV = 2845;

	/// <summary>The file with the parallelization information does not match to the currently used machine </summary>
	public const int Jl_ERR_AG_WH = 2846;

	/// <summary>Inconsistent knowledge base of Vision software agent </summary>
	public const int Jl_ERR_AG_KC = 2847;

	/// <summary>Unknown communication type </summary>
	public const int Jl_ERR_AG_CT = 2848;

	/// <summary>Unknown message type for Vision software agent </summary>
	public const int Jl_ERR_AG_MT = 2849;

	/// <summary>Error while saving the parallelization knowledge </summary>
	public const int Jl_ERR_AG_WK = 2850;

	/// <summary>Wrong type of work information </summary>
	public const int Jl_ERR_AG_WW = 2851;

	/// <summary>Wrong type of application information </summary>
	public const int Jl_ERR_AG_WA = 2852;

	/// <summary>Wrong type of experience information </summary>
	public const int Jl_ERR_AG_WE = 2853;

	/// <summary>Unknown name of Vision software agent </summary>
	public const int Jl_ERR_AG_NU = 2854;

	/// <summary>Unknown name and communication address of Vision software agent </summary>
	public const int Jl_ERR_AG_NE = 2855;

	/// <summary>cpu representative (Vision software agent) not reachable </summary>
	public const int Jl_ERR_AG_RR = 2856;

	/// <summary>cpu refuses work </summary>
	public const int Jl_ERR_AG_CR = 2857;

	/// <summary>Description of scheduling resource not found </summary>
	public const int Jl_ERR_AG_RN = 2858;

	/// <summary>Not accessible function of Vision software agent </summary>
	public const int Jl_ERR_AG_TILT = 2859;

	/// <summary>Wrong type: Vision scheduling resource </summary>
	public const int Jl_ERR_WRT = 2860;

	/// <summary>Wrong state: Vision scheduling resource </summary>
	public const int Jl_ERR_WRS = 2861;

	/// <summary>Unknown parameter type: Vision scheduling resource </summary>
	public const int Jl_ERR_UNKPT = 2862;

	/// <summary>Unknown parameter value: Vision scheduling resource </summary>
	public const int Jl_ERR_UNKPARVAL = 2863;

	/// <summary>Wrong post processing of control parameter </summary>
	public const int Jl_ERR_CTRL_WPP = 2864;

	/// <summary>Error while trying to get time </summary>
	public const int Jl_ERR_GETTI = 2867;

	/// <summary>Error while trying to get the number of processors </summary>
	public const int Jl_ERR_GETCPUNUM = 2868;

	/// <summary>Error while accessing temporary file </summary>
	public const int Jl_ERR_TMPFNF = 2869;

	/// <summary>message queue wait operation canceled </summary>
	public const int Jl_ERR_MQCNCL = 2890;

	/// <summary>message queue overflow </summary>
	public const int Jl_ERR_MQOVL = 2891;

	/// <summary>Threads still wait on message queue while * clearing it. </summary>
	public const int Jl_ERR_MQCLEAR = 2892;

	/// <summary>Invalid file format for a message </summary>
	public const int Jl_ERR_M_WRFILE = 2893;

	/// <summary>Dict does not contain requested key </summary>
	public const int Jl_ERR_DICT_KEY = 2894;

	/// <summary>Incorrect tuple length in dict </summary>
	public const int Jl_ERR_DICT_TUPLE_LENGTH = 2895;

	/// <summary>Incorrect tuple type in dict </summary>
	public const int Jl_ERR_DICT_TUPLE_TYPE = 2896;

	/// <summary>Invalid index for dict tuple </summary>
	public const int Jl_ERR_DICT_INVALID_INDEX = 2897;

	/// <summary>Dict is nested too deep </summary>
	public const int Jl_ERR_DICT_NESTED_TOO_DEEP = 2899;

	/// <summary>Error while forcing a context switch </summary>
	public const int Jl_ERR_PTHRD_SCHED = 2900;

	/// <summary>Error while accessing cpu affinity </summary>
	public const int Jl_ERR_SCHED_GAFF = 2901;

	/// <summary>Error while setting cpu affinity </summary>
	public const int Jl_ERR_SCHED_SAFF = 2902;

	/// <summary>wrong synchronization object </summary>
	public const int Jl_ERR_CO_WSO = 2950;

	/// <summary>wrong operator call object </summary>
	public const int Jl_ERR_CO_WOCO = 2952;

	/// <summary>input object not initialized </summary>
	public const int Jl_ERR_CO_IOPNI = 2953;

	/// <summary>input control not initialized </summary>
	public const int Jl_ERR_CO_ICPNI = 2954;

	/// <summary>output object not initialized </summary>
	public const int Jl_ERR_CO_OOPNI = 2955;

	/// <summary>output control not initialized </summary>
	public const int Jl_ERR_CO_OCPNI = 2956;

	/// <summary>Creation of pthread failed </summary>
	public const int Jl_ERR_PTHRD_CR = 2970;

	/// <summary>pthread-detach failed </summary>
	public const int Jl_ERR_PTHRD_DT = 2971;

	/// <summary>pthread-join failed </summary>
	public const int Jl_ERR_PTHRD_JO = 2972;

	/// <summary>Initialization of mutex variable failed </summary>
	public const int Jl_ERR_PTHRD_MI = 2973;

	/// <summary>Deletion of mutex variable failed </summary>
	public const int Jl_ERR_PTHRD_MD = 2974;

	/// <summary>Lock of mutex variable failed </summary>
	public const int Jl_ERR_PTHRD_ML = 2975;

	/// <summary>Unlock of mutex variable failed </summary>
	public const int Jl_ERR_PTHRD_MU = 2976;

	/// <summary>Failed to signal pthread condition var. </summary>
	public const int Jl_ERR_PTHRD_CS = 2977;

	/// <summary>Failed to wait for pthread cond. var. </summary>
	public const int Jl_ERR_PTHRD_CW = 2978;

	/// <summary>Failed to init pthread condition var. </summary>
	public const int Jl_ERR_PTHRD_CI = 2979;

	/// <summary>Failed to destroy pthread condition var.</summary>
	public const int Jl_ERR_PTHRD_CD = 2980;

	/// <summary>Failed to signal event. </summary>
	public const int Jl_ERR_PTHRD_ES = 2981;

	/// <summary>Failed to wait for event. </summary>
	public const int Jl_ERR_PTHRD_EW = 2982;

	/// <summary>Failed to init event. </summary>
	public const int Jl_ERR_PTHRD_EI = 2983;

	/// <summary>Failed to destroy event.</summary>
	public const int Jl_ERR_PTHRD_ED = 2984;

	/// <summary>Failed to create a tsd key.</summary>
	public const int Jl_ERR_PTHRD_TSDC = 2985;

	/// <summary>Failed to set a thread specific data key.</summary>
	public const int Jl_ERR_PTHRD_TSDS = 2986;

	/// <summary>Failed to get a tsd key.</summary>
	public const int Jl_ERR_PTHRD_TSDG = 2987;

	/// <summary>Failed to free a tsd key.</summary>
	public const int Jl_ERR_PTHRD_TSDF = 2988;

	/// <summary>Aborted waiting at a barrier</summary>
	public const int Jl_ERR_PTHRD_BA = 2989;

	/// <summary>'Free list' is empty while scheduling </summary>
	public const int Jl_ERR_DCDG_FLE = 2990;

	/// <summary>Communication partner not checked in </summary>
	public const int Jl_ERR_MSG_PNCI = 2991;

	/// <summary>The communication system can't be started while running </summary>
	public const int Jl_ERR_MSG_CSAI = 2992;

	/// <summary>Communication partner not checked in </summary>
	public const int Jl_ERR_MSG_CSNI = 2993;

	/// <summary>Initialization of Barrier failed </summary>
	public const int Jl_ERR_PTHRD_BI = 2994;

	/// <summary>Waiting at a barrier failed </summary>
	public const int Jl_ERR_PTHRD_BW = 2995;

	/// <summary>Destroying of an barrier failed </summary>
	public const int Jl_ERR_PTHRD_BD = 2996;

	/// <summary>Region completely outside of the image domain </summary>
	public const int Jl_ERR_RCOIMA = 3010;

	/// <summary>Region (partially) outside of the definition range of the image </summary>
	public const int Jl_ERR_ROOIMA = 3011;

	/// <summary>Intersected definition range region/image empty </summary>
	public const int Jl_ERR_RIEI = 3012;

	/// <summary>Image with empty definition range </summary>
	public const int Jl_ERR_EDEF = 3013;

	/// <summary>No common image point of two images </summary>
	public const int Jl_ERR_IIEI = 3014;

	/// <summary>Wrong region for image (first row &lt; 0) </summary>
	public const int Jl_ERR_FLTS = 3015;

	/// <summary>Wrong region for image (column in last row &gt;= image width) </summary>
	public const int Jl_ERR_LLTB = 3016;

	/// <summary>Number of images unequal in input pars. </summary>
	public const int Jl_ERR_UENOI = 3017;

	/// <summary>Image height too small </summary>
	public const int Jl_ERR_HTS = 3018;

	/// <summary>Image width too small </summary>
	public const int Jl_ERR_WTS = 3019;

	/// <summary>Internal error: Multiple call of JlRLInitSeg() </summary>
	public const int Jl_ERR_CHSEG = 3020;

	/// <summary>Internal error: JlRLSeg() not initialized </summary>
	public const int Jl_ERR_RLSEG1 = 3021;

	/// <summary>Wrong size of filter for Gauss </summary>
	public const int Jl_ERR_WGAUSSM = 3022;

	/// <summary>Filter size exceeds image size </summary>
	public const int Jl_ERR_FSEIS = 3033;

	/// <summary>Filter size evan </summary>
	public const int Jl_ERR_FSEVAN = 3034;

	/// <summary>Filter size to big </summary>
	public const int Jl_ERR_FSTOBIG = 3035;

	/// <summary>Region is empty </summary>
	public const int Jl_ERR_EMPTREG = 3036;

	/// <summary>Domains of the input images differ </summary>
	public const int Jl_ERR_DOM_DIFF = 3037;

	/// <summary>Row value of a coordinate &gt; 2^15-1 (XL: 2^30 - 1) </summary>
	public const int Jl_ERR_ROWTB = 3040;

	/// <summary>Row value of a coordinate &lt; -2^15+1 (XL: -2^30+1) </summary>
	public const int Jl_ERR_ROWTS = 3041;

	/// <summary>Column value of a coordinate &gt; 2^15-1 (XL: 2^30 - 1) </summary>
	public const int Jl_ERR_COLTB = 3042;

	/// <summary>Column value of a coordinate &lt; -2^15+1 (XL: -2^30+1) </summary>
	public const int Jl_ERR_COLTS = 3043;

	/// <summary>Wrong segmentation threshold </summary>
	public const int Jl_ERR_WRTHR = 3100;

	/// <summary>Unknown feature </summary>
	public const int Jl_ERR_UNKF = 3101;

	/// <summary>Unknown gray value feature </summary>
	public const int Jl_ERR_UNKG = 3102;

	/// <summary>Internal error in JlContCut </summary>
	public const int Jl_ERR_EINCC = 3103;

	/// <summary>Error in JlContToPol: distance of points too big </summary>
	public const int Jl_ERR_EINCP1 = 3104;

	/// <summary>Error in JlContToPol: contour too long </summary>
	public const int Jl_ERR_EINCP2 = 3105;

	/// <summary>Too many rows (IPImageTransform) </summary>
	public const int Jl_ERR_TMR = 3106;

	/// <summary>Scaling factor = 0.0 (IPImageScale) </summary>
	public const int Jl_ERR_SFZ = 3107;

	/// <summary>Wrong range in transformation matrix </summary>
	public const int Jl_ERR_OOR = 3108;

	/// <summary>Internal error in IPvvf: no element free </summary>
	public const int Jl_ERR_NEF = 3109;

	/// <summary>Number of input objects is zero </summary>
	public const int Jl_ERR_NOOB = 3110;

	/// <summary>At least one input object has an empty region </summary>
	public const int Jl_ERR_EMPOB = 3111;

	/// <summary>Operation allowed for rectangular images 2**n only </summary>
	public const int Jl_ERR_NPOT = 3112;

	/// <summary>Too many relevant points (IPHysterese) </summary>
	public const int Jl_ERR_TMEP = 3113;

	/// <summary>Number of labels in image too big </summary>
	public const int Jl_ERR_LTB = 3114;

	/// <summary>No labels with negative values allowed </summary>
	public const int Jl_ERR_NNLA = 3115;

	/// <summary>Wrong filter size (too small ?) </summary>
	public const int Jl_ERR_WFS = 3116;

	/// <summary>Images with different image size </summary>
	public const int Jl_ERR_IWDS = 3117;

	/// <summary>Target image too wide or too far on the right </summary>
	public const int Jl_ERR_IWTL = 3118;

	/// <summary>Target image too narrow or too far on the left </summary>
	public const int Jl_ERR_IWTS = 3119;

	/// <summary>Target image too high or too far down </summary>
	public const int Jl_ERR_IHTL = 3120;

	/// <summary>Target image too low or too far up </summary>
	public const int Jl_ERR_IHTS = 3121;

	/// <summary>Number of channels in the input parameters are different </summary>
	public const int Jl_ERR_DNOC = 3122;

	/// <summary>Wrong color filter array type </summary>
	public const int Jl_ERR_WRCFAFLT = 3123;

	/// <summary>Wrong color filter array interpolation </summary>
	public const int Jl_ERR_WRCFAINT = 3124;

	/// <summary>Homogeneous matrix does not represent an affine transformation </summary>
	public const int Jl_ERR_NO_AFFTRANS = 3125;

	/// <summary>Inpainting region too close to the image border </summary>
	public const int Jl_ERR_INPNOBDRY = 3126;

	/// <summary>source and destination differ in size</summary>
	public const int Jl_ERR_DSIZESD = 3127;

	/// <summary>Reflection axis undefined </summary>
	public const int Jl_ERR_AXIS_UNDEF = 3129;

	/// <summary>Coocurrence Matrix: Too little columns for quantisation </summary>
	public const int Jl_ERR_COWTS = 3131;

	/// <summary>Coocurrence Matrix: Too little rows for quantisation </summary>
	public const int Jl_ERR_COHTS = 3132;

	/// <summary>Wrong number of columns </summary>
	public const int Jl_ERR_NUM_COLMN = 3133;

	/// <summary>Wrong number of rows </summary>
	public const int Jl_ERR_NUM_LINES = 3134;

	/// <summary>Number has too many digits </summary>
	public const int Jl_ERR_OVL = 3135;

	/// <summary>Matrix is not symmetric </summary>
	public const int Jl_ERR_NOT_SYM = 3136;

	/// <summary>Matrix is too big </summary>
	public const int Jl_ERR_NUM_COLS = 3137;

	/// <summary>Wrong structure of file </summary>
	public const int Jl_ERR_SYNTAX = 3138;

	/// <summary>Less than 2 matrices </summary>
	public const int Jl_ERR_MISSING = 3139;

	/// <summary>Not enough memory </summary>
	public const int Jl_ERR_COOC_MEM = 3140;

	/// <summary>Can not read the file </summary>
	public const int Jl_ERR_NO_FILE = 3141;

	/// <summary>Can not open file for writing </summary>
	public const int Jl_ERR_FILE_WR = 3142;

	/// <summary>Too many lookup table colors </summary>
	public const int Jl_ERR_NUM_LUCOLS = 3143;

	/// <summary>Too many Hough points (lines) </summary>
	public const int Jl_ERR_WNOLI = 3145;

	/// <summary>Target image has got wrong height (not big enough) </summary>
	public const int Jl_ERR_DITS = 3146;

	/// <summary>Wrong interpolation mode </summary>
	public const int Jl_ERR_WINTM = 3147;

	/// <summary>Region not compact or not connected </summary>
	public const int Jl_ERR_THICK_NK = 3148;

	/// <summary>Wrong filter index for filter size 3 </summary>
	public const int Jl_ERR_WIND3 = 3170;

	/// <summary>Wrong filter index for filter size 5 </summary>
	public const int Jl_ERR_WIND5 = 3171;

	/// <summary>Wrong filter index for filter size 7 </summary>
	public const int Jl_ERR_WIND7 = 3172;

	/// <summary>Wrong filter size; only 3/5/7 </summary>
	public const int Jl_ERR_WLAWSS = 3173;

	/// <summary>Number of suitable pixels too small to reliably estimate the noise </summary>
	public const int Jl_ERR_NE_NPTS = 3175;

	/// <summary>Different number of entries/exits in JlContCut </summary>
	public const int Jl_ERR_WNEE = 3200;

	/// <summary>Reference to contour is missing </summary>
	public const int Jl_ERR_REF = 3201;

	/// <summary>Wrong XLD type </summary>
	public const int Jl_ERR_XLDWT = 3250;

	/// <summary>Border point is set to FG </summary>
	public const int Jl_ERR_XLD_RPF = 3252;

	/// <summary>Maximum contour length exceeded </summary>
	public const int Jl_ERR_XLD_MCL = 3253;

	/// <summary>Maximum number of contours exceeded </summary>
	public const int Jl_ERR_XLD_MCN = 3254;

	/// <summary>Contour too short for fetch_angle_xld </summary>
	public const int Jl_ERR_XLD_CTS = 3255;

	/// <summary>Regression parameters of contours already computed </summary>
	public const int Jl_ERR_XLD_CRD = 3256;

	/// <summary>Regression parameters of contours not yet entered! </summary>
	public const int Jl_ERR_XLD_CRND = 3257;

	/// <summary>Data base: XLD object has been deleted </summary>
	public const int Jl_ERR_DBXC = 3258;

	/// <summary>Data base: Object has no XLD-ID </summary>
	public const int Jl_ERR_DBWXID = 3259;

	/// <summary>Wrong number of contour points allocated </summary>
	public const int Jl_ERR_XLD_WNP = 3260;

	/// <summary>Contour attribute not defined </summary>
	public const int Jl_ERR_XLD_CAND = 3261;

	/// <summary>Ellipse fitting failed </summary>
	public const int Jl_ERR_FIT_ELLIPSE = 3262;

	/// <summary>Circle fitting failed </summary>
	public const int Jl_ERR_FIT_CIRCLE = 3263;

	/// <summary>All points classified as outliers (ClippingFactor too small or used points not similar to primitive) </summary>
	public const int Jl_ERR_FIT_CLIP = 3264;

	/// <summary>Quadrangle fitting failed </summary>
	public const int Jl_ERR_FIT_QUADRANGLE = 3265;

	/// <summary>No points for at least one rectangle side </summary>
	public const int Jl_ERR_INCOMPL_RECT = 3266;

	/// <summary>A contour point lies outside of the image </summary>
	public const int Jl_ERR_XLD_COI = 3267;

	/// <summary>Not enough points for model fitting </summary>
	public const int Jl_ERR_FIT_NOT_ENOUGH_POINTS = 3274;

	/// <summary>No ARC/INFO world file </summary>
	public const int Jl_ERR_NWF = 3275;

	/// <summary>No ARC/INFO generate file </summary>
	public const int Jl_ERR_NAIGF = 3276;

	/// <summary>Unexpected end of file while reading DXF file </summary>
	public const int Jl_ERR_DXF_UEOF = 3278;

	/// <summary>Cannot read DXF-group code from file </summary>
	public const int Jl_ERR_DXF_CRGC = 3279;

	/// <summary>Inconsistent number of attributes per point in DXF file </summary>
	public const int Jl_ERR_DXF_INAPP = 3280;

	/// <summary>Inconsistent number of attributes and names in DXF file </summary>
	public const int Jl_ERR_DXF_INAPPN = 3281;

	/// <summary>Inconsistent number of global attributes and names in DXF file </summary>
	public const int Jl_ERR_DXF_INAPCN = 3282;

	/// <summary>Cannot read attributes from DXF file </summary>
	public const int Jl_ERR_DXF_CRAPP = 3283;

	/// <summary>Cannot read global attributes from DXF file </summary>
	public const int Jl_ERR_DXF_CRAPC = 3284;

	/// <summary>Cannot read attribute names from DXF file </summary>
	public const int Jl_ERR_DXF_CRAN = 3285;

	/// <summary>Wrong generic parameter name </summary>
	public const int Jl_ERR_DXF_WPN = 3286;

	/// <summary>Internal DXF I/O error: Wrong data type </summary>
	public const int Jl_ERR_DXF_IEDT = 3289;

	/// <summary>Isolated point while contour merging </summary>
	public const int Jl_ERR_XLD_ISOL_POINT = 3290;

	/// <summary>Constraints cannot be fulfilled </summary>
	public const int Jl_ERR_NURBS_CCBF = 3291;

	/// <summary>No segment in contour </summary>
	public const int Jl_ERR_NSEG = 3292;

	/// <summary>Only one or no point in template contour </summary>
	public const int Jl_ERR_NO_ONE_P = 3293;

	/// <summary>Maximum number of attributes per example exceeded </summary>
	public const int Jl_ERR_TMFE = 3301;

	/// <summary>Too many examples for one data set for training </summary>
	public const int Jl_ERR_TMSAM = 3305;

	/// <summary>Too many classes </summary>
	public const int Jl_ERR_TMCLS = 3306;

	/// <summary>Maximum number of cuboids exceeded </summary>
	public const int Jl_ERR_TMBOX = 3307;

	/// <summary>Wrong id in classification file </summary>
	public const int Jl_ERR_CLASS2_ID = 3316;

	/// <summary>The version of the classifier is not supported </summary>
	public const int Jl_ERR_CLASS2_VERS = 3317;

	/// <summary>Text model does not contain a classifier yet (use set_text_model_param) </summary>
	public const int Jl_ERR_TM_NO_CL = 3319;

	/// <summary>Error in KMeans cluter initialization. </summary>
	public const int Jl_ERR_ML_KMEAN_INITIALIZATION_ERROR = 3325;

	/// <summary>Invalid file format for GMM training samples </summary>
	public const int Jl_ERR_GMM_NOTRAINFILE = 3330;

	/// <summary>The version of the GMM training samples is not supported </summary>
	public const int Jl_ERR_GMM_WRTRAINVERS = 3331;

	/// <summary>Wrong training sample file format </summary>
	public const int Jl_ERR_GMM_WRSMPFORMAT = 3332;

	/// <summary>nvalid file format for Gaussian Mixture Model (GMM) </summary>
	public const int Jl_ERR_GMM_NOCLASSFILE = 3333;

	/// <summary>The version of the Gaussian Mixture Model (GMM) is not supported </summary>
	public const int Jl_ERR_GMM_WRCLASSVERS = 3334;

	/// <summary>Unknown error when training GMM </summary>
	public const int Jl_ERR_GMM_TRAIN_UNKERR = 3335;

	/// <summary>Collapsed covariance matrix </summary>
	public const int Jl_ERR_GMM_TRAIN_COLLAPSED = 3336;

	/// <summary>No samples for at least one class </summary>
	public const int Jl_ERR_GMM_TRAIN_NOSAMPLE = 3337;

	/// <summary>Too few samples for at least one class </summary>
	public const int Jl_ERR_GMM_TRAIN_FEWSAMPLES = 3338;

	/// <summary>GMM is not trained </summary>
	public const int Jl_ERR_GMM_NOTTRAINED = 3340;

	/// <summary>GMM has no training data </summary>
	public const int Jl_ERR_GMM_NOTRAINDATA = 3341;

	/// <summary>Serialized item does not contain a valid Gaussian Mixture Model (GMM) </summary>
	public const int Jl_ERR_GMM_NOSITEM = 3342;

	/// <summary>Unknown output function </summary>
	public const int Jl_ERR_MLP_UNKOUTFUNC = 3350;

	/// <summary>Target not in 0-1 encoding </summary>
	public const int Jl_ERR_MLP_NOT01ENC = 3351;

	/// <summary>No training samples stored in the classifier </summary>
	public const int Jl_ERR_MLP_NOTRAINDATA = 3352;

	/// <summary>Invalid file format for MLP training samples </summary>
	public const int Jl_ERR_MLP_NOTRAINFILE = 3353;

	/// <summary>The version of the MLP training samples is not supported </summary>
	public const int Jl_ERR_MLP_WRTRAINVERS = 3354;

	/// <summary>Wrong training sample format </summary>
	public const int Jl_ERR_MLP_WRSMPFORMAT = 3355;

	/// <summary>MLP is not a classifier </summary>
	public const int Jl_ERR_MLP_NOCLASSIF = 3356;

	/// <summary>Invalid file format for multilayer perceptron (MLP) </summary>
	public const int Jl_ERR_MLP_NOCLASSFILE = 3357;

	/// <summary>The version of the multilayer perceptron (MLP) is not supported </summary>
	public const int Jl_ERR_MLP_WRCLASSVERS = 3358;

	/// <summary>Wrong number of channels </summary>
	public const int Jl_ERR_WRNUMCHAN = 3359;

	/// <summary>Wrong number of MLP parameters </summary>
	public const int Jl_ERR_MLP_WRNUMPARAM = 3360;

	/// <summary>Serialized item does not contain a valid multilayer perceptron (MLP) </summary>
	public const int Jl_ERR_MLP_NOSITEM = 3361;

	/// <summary>The number of image channels and the number of dimensions of the look-up table do not match </summary>
	public const int Jl_ERR_LUT_WRNUMCHAN = 3370;

	/// <summary>A look-up table can be build for 2 or 3 channels only </summary>
	public const int Jl_ERR_LUT_NRCHANLARGE = 3371;

	/// <summary>Cannot create look-up table. Please choose a larger 'bit_depth' or select the 'fast' 'class_selection'. </summary>
	public const int Jl_ERR_LUT_CANNOTCREAT = 3372;

	/// <summary>No training samples stored in the classifier </summary>
	public const int Jl_ERR_SVM_NOTRAINDATA = 3380;

	/// <summary>Invalid file format for SVM training samples </summary>
	public const int Jl_ERR_SVM_NOTRAINFILE = 3381;

	/// <summary>The version of the SVM training samples is not supported </summary>
	public const int Jl_ERR_SVM_WRTRAINVERS = 3382;

	/// <summary>Wrong training sample format </summary>
	public const int Jl_ERR_SVM_WRSMPFORMAT = 3383;

	/// <summary>Invalid file format for support vector machine (SVM) </summary>
	public const int Jl_ERR_SVM_NOCLASSFILE = 3384;

	/// <summary>The version of the support vector machine (SVM) is not supported </summary>
	public const int Jl_ERR_SVM_WRCLASSVERS = 3385;

	/// <summary>Wrong number of classes </summary>
	public const int Jl_ERR_SVM_WRNRCLASS = 3386;

	/// <summary>Chosen nu is too big </summary>
	public const int Jl_ERR_SVM_NU_TOO_BIG = 3387;

	/// <summary>SVM Training failed </summary>
	public const int Jl_ERR_SVM_TRAIN_FAIL = 3388;

	/// <summary>SVMs do not fit together </summary>
	public const int Jl_ERR_SVM_DO_NOT_FIT = 3389;

	/// <summary>No SV in SVM to add to training </summary>
	public const int Jl_ERR_SVM_NO_TRAIN_ADD = 3390;

	/// <summary>Kernel must be RBF </summary>
	public const int Jl_ERR_SVM_KERNELNOTRBF = 3391;

	/// <summary>Not all classes contained in training data </summary>
	public const int Jl_ERR_SVM_NO_TRAIND_FOR_CLASS = 3392;

	/// <summary>SVM not trained </summary>
	public const int Jl_ERR_SVM_NOT_TRAINED = 3393;

	/// <summary>Classifier not trained </summary>
	public const int Jl_ERR_NOT_TRAINED = 3394;

	/// <summary>Serialized item does not contain a valid support vector machine (SVM) </summary>
	public const int Jl_ERR_SVM_NOSITEM = 3395;

	/// <summary>Wrong rotation number </summary>
	public const int Jl_ERR_ROTNR = 3401;

	/// <summary>Wrong letter for Golay element </summary>
	public const int Jl_ERR_GOL = 3402;

	/// <summary>Wrong reference point </summary>
	public const int Jl_ERR_BEZ = 3403;

	/// <summary>Wrong number of iterations </summary>
	public const int Jl_ERR_ITER = 3404;

	/// <summary>Mophology: system error </summary>
	public const int Jl_ERR_MOSYS = 3405;

	/// <summary>Wrong type of boundary </summary>
	public const int Jl_ERR_ART = 3406;

	/// <summary>Morphology: Wrong number of input obj. </summary>
	public const int Jl_ERR_OBJI = 3407;

	/// <summary>Morphology: Wrong number of output obj. </summary>
	public const int Jl_ERR_OBJO = 3408;

	/// <summary>Morphology: Wrong number of input control parameter </summary>
	public const int Jl_ERR_PARI = 3409;

	/// <summary>Morphology: Wrong number of output control parameter </summary>
	public const int Jl_ERR_PARO = 3410;

	/// <summary>Morphology: Struct. element is infinite </summary>
	public const int Jl_ERR_SELC = 3411;

	/// <summary>Morphology: Wrong name for struct. elem. </summary>
	public const int Jl_ERR_WRNSE = 3412;

	/// <summary>Wrong number of run length rows (chords): smaller than 0 </summary>
	public const int Jl_ERR_WRRLN1 = 3500;

	/// <summary>Number of chords too big, increase * current_runlength_number using set_system</summary>
	public const int Jl_ERR_WRRLN2 = 3501;

	/// <summary>Run length row with negative length </summary>
	public const int Jl_ERR_WRRLL = 3502;

	/// <summary>Run length row &gt;= image height </summary>
	public const int Jl_ERR_RLLTB = 3503;

	/// <summary>Run length row &lt; 0 </summary>
	public const int Jl_ERR_RLLTS = 3504;

	/// <summary>Run length column &gt;= image width </summary>
	public const int Jl_ERR_RLCTB = 3505;

	/// <summary>Lauflaengenspalte &lt; 0 </summary>
	public const int Jl_ERR_RLCTS = 3506;

	/// <summary>For CHORD_TYPE: Number of row too big </summary>
	public const int Jl_ERR_CHLTB = 3507;

	/// <summary>For CHORD_TYPE: Number of row too small </summary>
	public const int Jl_ERR_CHLTS = 3508;

	/// <summary>For CHORD_TYPE: Number of column too big </summary>
	public const int Jl_ERR_CHCTB = 3509;

	/// <summary>Exceeding the maximum number of run lengths while automatic expansion </summary>
	public const int Jl_ERR_MRLE = 3510;

	/// <summary>Region-&gt;compl neither true/false </summary>
	public const int Jl_ERR_ICCOMPL = 3511;

	/// <summary>Region-&gt;max_num &lt; Region-&gt;num </summary>
	public const int Jl_ERR_RLEMAX = 3512;

	/// <summary>Number of chords too big for num_max </summary>
	public const int Jl_ERR_WRRLN3 = 3513;

	/// <summary>Operator cannot be implemented for complemented regions </summary>
	public const int Jl_ERR_OPNOCOMPL = 3514;

	/// <summary>Image width &lt; 0 </summary>
	public const int Jl_ERR_WIMAW1 = 3520;

	/// <summary>Image width &gt;= MAX_FORMAT </summary>
	public const int Jl_ERR_WIMAW2 = 3521;

	/// <summary>Image height &lt;= 0 </summary>
	public const int Jl_ERR_WIMAH1 = 3522;

	/// <summary>Image height &gt;= MAX_FORMAT </summary>
	public const int Jl_ERR_WIMAH2 = 3523;

	/// <summary>Image width &lt;= 0 </summary>
	public const int Jl_ERR_WIMAW3 = 3524;

	/// <summary>Image height &lt;= 0 </summary>
	public const int Jl_ERR_WIMAH3 = 3525;

	/// <summary>Too many segments </summary>
	public const int Jl_ERR_TMS = 3550;

	/// <summary>INT8 images are available on 64 bit systems only </summary>
	public const int Jl_ERR_NO_INT8_IMAGE = 3551;

	/// <summary>Point at infinity cannot be converted to a Euclidean point </summary>
	public const int Jl_ERR_POINT_AT_INFINITY = 3600;

	/// <summary>Covariance matrix could not be determined </summary>
	public const int Jl_ERR_ML_NO_COVARIANCE = 3601;

	/// <summary>RANSAC algorithm didn't find enough point correspondences </summary>
	public const int Jl_ERR_RANSAC_PRNG = 3602;

	/// <summary>RANSAC algorithm didn't find enough point correspondences </summary>
	public const int Jl_ERR_RANSAC_TOO_DIFFERENT = 3603;

	/// <summary>Internal diagnosis: fallback method had to be used </summary>
	public const int Jl_ERR_PTI_FALLBACK = 3604;

	/// <summary>Projective transformation is singular </summary>
	public const int Jl_ERR_PTI_TRAFO_SING = 3605;

	/// <summary>Mosaic is under-determined </summary>
	public const int Jl_ERR_PTI_MOSAIC_UNDERDET = 3606;

	/// <summary>Input covariance matrix is not positive definite </summary>
	public const int Jl_ERR_COV_NPD = 3607;

	/// <summary>The number of input points too large. </summary>
	public const int Jl_ERR_TOO_MANY_POINTS = 3608;

	/// <summary>Inconsistent number of point correspondences. </summary>
	public const int Jl_ERR_INPC = 3620;

	/// <summary>No path from reference image to one or more images. </summary>
	public const int Jl_ERR_NOPA = 3621;

	/// <summary>Image with specified index does not exist. </summary>
	public const int Jl_ERR_IINE = 3622;

	/// <summary>Matrix is not a camera matrix. </summary>
	public const int Jl_ERR_NOCM = 3623;

	/// <summary>Skew is not zero. </summary>
	public const int Jl_ERR_SKNZ = 3624;

	/// <summary>Illegal focal length. </summary>
	public const int Jl_ERR_ILFL = 3625;

	/// <summary>Kappa is not zero. </summary>
	public const int Jl_ERR_KANZ = 3626;

	/// <summary>It is not possible to determine all parameters for in the variable case. </summary>
	public const int Jl_ERR_VARA = 3627;

	/// <summary>No valid implementation selected. </summary>
	public const int Jl_ERR_LVDE = 3628;

	/// <summary>Kappa can only be determined with the gold-standard method for fixed camera parameters. </summary>
	public const int Jl_ERR_KPAR = 3629;

	/// <summary>Conflicting number of images and projection mode. </summary>
	public const int Jl_ERR_IMOD = 3630;

	/// <summary>Error in projection: Point not in any cube map. </summary>
	public const int Jl_ERR_PNIC = 3631;

	/// <summary>No solution found. </summary>
	public const int Jl_ERR_NO_SOL = 3632;

	/// <summary>Tilt is not zero. </summary>
	public const int Jl_ERR_TINZ = 3633;

	/// <summary>Illegal combination of parameters and estimation method. </summary>
	public const int Jl_ERR_ILMD = 3640;

	/// <summary>No suitable contours found </summary>
	public const int Jl_ERR_RDS_NSC = 3660;

	/// <summary>No stable solution found </summary>
	public const int Jl_ERR_RDS_NSS = 3661;

	/// <summary>Instable solution found </summary>
	public const int Jl_ERR_RDS_ISS = 3662;

	/// <summary>Not enough contours for calibration </summary>
	public const int Jl_ERR_RDS_NEC = 3663;

	/// <summary>Invalid file format for FFT optimization data </summary>
	public const int Jl_ERR_NOFFTOPT = 3650;

	/// <summary>The version of the FFT optimization data is not supported </summary>
	public const int Jl_ERR_WRFFTOPTVERS = 3651;

	/// <summary>Optimization data was created with a different Vision version (Standard Vision / Parallel Vision) </summary>
	public const int Jl_ERR_WRVisionVERS = 3652;

	/// <summary>Storing of the optimization data failed </summary>
	public const int Jl_ERR_OPTFAIL = 3653;

	/// <summary>Serialized item does not contain valid FFT optimization data </summary>
	public const int Jl_ERR_FFTOPT_NOSITEM = 3654;

	/// <summary>Invalid disparity range for binocular_disparity_ms method </summary>
	public const int Jl_ERR_INVLD_DISP_RANGE = 3690;

	/// <summary>Epipoles are situated within the image domain </summary>
	public const int Jl_ERR_EPIINIM = 3700;

	/// <summary>Fields of view of both cameras do not intersect each other </summary>
	public const int Jl_ERR_EPI_FOV = 3701;

	/// <summary>Rectification impossible </summary>
	public const int Jl_ERR_EPI_RECT = 3702;

	/// <summary>Wrong type of target_thickness parameter </summary>
	public const int Jl_ERR_BI_WT_TARGET = 3710;

	/// <summary>Wrong type of thickness_tolerance parameter </summary>
	public const int Jl_ERR_BI_WT_THICKNESS = 3711;

	/// <summary>Wrong type of position_tolerance parameter </summary>
	public const int Jl_ERR_BI_WT_POSITION = 3712;

	/// <summary>Wrong type of sigma parameter </summary>
	public const int Jl_ERR_BI_WT_SIGMA = 3713;

	/// <summary>Wrong value of sigma parameter </summary>
	public const int Jl_ERR_BI_WV_SIGMA = 3714;

	/// <summary>Wrong type of threshold parameter </summary>
	public const int Jl_ERR_BI_WT_THRESH = 3715;

	/// <summary>Wrong value of target_thickness parameter </summary>
	public const int Jl_ERR_BI_WV_TARGET = 3716;

	/// <summary>Wrong value of thickness_tolerance parameter </summary>
	public const int Jl_ERR_BI_WV_THICKNESS = 3717;

	/// <summary>Wrong value of position_tolerance parameter </summary>
	public const int Jl_ERR_BI_WV_POSITION = 3718;

	/// <summary>Wrong value of threshold parameter </summary>
	public const int Jl_ERR_BI_WV_THRESH = 3719;

	/// <summary>Wrong type of refinement parameter </summary>
	public const int Jl_ERR_BI_WT_REFINE = 3720;

	/// <summary>Wrong value of refinement parameter </summary>
	public const int Jl_ERR_BI_WV_REFINE = 3721;

	/// <summary>Wrong type of resolution parameter </summary>
	public const int Jl_ERR_BI_WT_RESOL = 3722;

	/// <summary>Wrong type of resolution parameter </summary>
	public const int Jl_ERR_BI_WV_RESOL = 3723;

	/// <summary>Wrong type of polarity parameter </summary>
	public const int Jl_ERR_BI_WT_POLARITY = 3724;

	/// <summary>Wrong type of polarity parameter </summary>
	public const int Jl_ERR_BI_WV_POLARITY = 3725;

	/// <summary>No sheet-of-light model available</summary>
	public const int Jl_ERR_SOL_EMPTY_MODEL_LIST = 3751;

	/// <summary>Wrong input image size (width) </summary>
	public const int Jl_ERR_SOL_WNIW = 3752;

	/// <summary>Wrong input image size (height) </summary>
	public const int Jl_ERR_SOL_WNIH = 3753;

	/// <summary>profile region does not fit the domain of definition of the input image </summary>
	public const int Jl_ERR_SOL_WPROF_REG = 3754;

	/// <summary>Calibration extend not set </summary>
	public const int Jl_ERR_SOL_CAL_NONE = 3755;

	/// <summary>Undefined disparity image </summary>
	public const int Jl_ERR_SOL_UNDEF_DISPARITY = 3756;

	/// <summary>Undefined domain for disparity image </summary>
	public const int Jl_ERR_SOL_UNDEF_DISPDOMAIN = 3757;

	/// <summary>Undefined camera parameter </summary>
	public const int Jl_ERR_SOL_UNDEF_CAMPAR = 3758;

	/// <summary>Undefined pose of the lightplane </summary>
	public const int Jl_ERR_SOL_UNDEF_LPCS = 3759;

	/// <summary>Undefined pose of the camera coordinate system </summary>
	public const int Jl_ERR_SOL_UNDEF_CCS = 3760;

	/// <summary>Undefined transformation from the camera to the lightplane coordinate system </summary>
	public const int Jl_ERR_SOL_UNDEF_CCS_2_LPCS = 3761;

	/// <summary>Undefined movement pose for xyz calibration </summary>
	public const int Jl_ERR_SOL_UNDEF_MOV_POSE = 3762;

	/// <summary>Wrong value of scale parameter </summary>
	public const int Jl_ERR_SOL_WV_SCALE = 3763;

	/// <summary>Wrong parameter name </summary>
	public const int Jl_ERR_SOL_WV_PAR_NAME = 3764;

	/// <summary>Wrong type of parameter method </summary>
	public const int Jl_ERR_SOL_WT_METHOD = 3765;

	/// <summary>Wrong type of parameter ambiguity </summary>
	public const int Jl_ERR_SOL_WT_AMBIGUITY = 3766;

	/// <summary>Wrong type of parameter score </summary>
	public const int Jl_ERR_SOL_WT_SCORE_TYPE = 3767;

	/// <summary>Wrong type of parameter calibration </summary>
	public const int Jl_ERR_SOL_WT_CALIBRATION = 3768;

	/// <summary>Wrong type of parameter number_profiles </summary>
	public const int Jl_ERR_SOL_WT_NUM_PROF = 3769;

	/// <summary>Wrong type of element in parameter camera_parameter </summary>
	public const int Jl_ERR_SOL_WT_CAM_PAR = 3770;

	/// <summary>Wrong type of element in pose </summary>
	public const int Jl_ERR_SOL_WT_PAR_POSE = 3771;

	/// <summary>Wrong value of parameter method </summary>
	public const int Jl_ERR_SOL_WV_METHOD = 3772;

	/// <summary>Wrong type of parameter min_gray </summary>
	public const int Jl_ERR_SOL_WT_THRES = 3773;

	/// <summary>Wrong value of parameter ambiguity </summary>
	public const int Jl_ERR_SOL_WV_AMBIGUITY = 3774;

	/// <summary>Wrong value of parameter score_type </summary>
	public const int Jl_ERR_SOL_WV_SCORE_TYPE = 3775;

	/// <summary>Wrong value of parameter calibration </summary>
	public const int Jl_ERR_SOL_WV_CALIBRATION = 3776;

	/// <summary>Wrong value of parameter number_profiles </summary>
	public const int Jl_ERR_SOL_WV_NUM_PROF = 3777;

	/// <summary>Wrong type of camera </summary>
	public const int Jl_ERR_SOL_WV_CAMERA_TYPE = 3778;

	/// <summary>Wrong number of values of parameter camera_parameter </summary>
	public const int Jl_ERR_SOL_WN_CAM_PAR = 3779;

	/// <summary>Wrong number of values of pose </summary>
	public const int Jl_ERR_SOL_WN_POSE = 3780;

	/// <summary>Calibration target not found </summary>
	public const int Jl_ERR_SOL_NO_TARGET_FOUND = 3781;

	/// <summary>The calibration algorithm failed to find a valid solution. </summary>
	public const int Jl_ERR_SOL_NO_VALID_SOL = 3782;

	/// <summary>Wrong type of parameter calibration_object </summary>
	public const int Jl_ERR_SOL_WT_CALIB_OBJECT = 3783;

	/// <summary>Invalid calibration object </summary>
	public const int Jl_ERR_SOL_INVALID_CALIB_OBJECT = 3784;

	/// <summary>No calibration object set </summary>
	public const int Jl_ERR_SOL_NO_CALIB_OBJECT_SET = 3785;

	/// <summary>Invalid file format for sheet-of-light model </summary>
	public const int Jl_ERR_SOL_WR_FILE_FORMAT = 3786;

	/// <summary>The version of the sheet-of-light model is not supported </summary>
	public const int Jl_ERR_SOL_WR_FILE_VERS = 3787;

	/// <summary>Camera type not supported by calibrate_sheet_of_light_model</summary>
	public const int Jl_ERR_SOL_CAMPAR_UNSUPPORTED = 3788;

	/// <summary>Parameter does not match the set 'calibration' </summary>
	public const int Jl_ERR_SOL_PAR_CALIB = 3790;

	/// <summary>The gray values of the disparity image do not fit the height of the camera </summary>
	public const int Jl_ERR_SOL_WGV_DISP = 3791;

	/// <summary>Wrong texture inspection model type</summary>
	public const int Jl_ERR_TI_WRONGMODEL = 3800;

	/// <summary>Texture Model is not trained </summary>
	public const int Jl_ERR_TI_NOTTRAINED = 3801;

	/// <summary>Texture Model has no training data </summary>
	public const int Jl_ERR_TI_NOTRAINDATA = 3802;

	/// <summary>Invalid file format for Texture inspection model </summary>
	public const int Jl_ERR_TI_NOTRAINFILE = 3803;

	/// <summary>The version of the Texture inspection model is not supported </summary>
	public const int Jl_ERR_TI_WRTRAINVERS = 3804;

	/// <summary>Wrong training sample file format </summary>
	public const int Jl_ERR_TI_WRSMPFORMAT = 3805;

	/// <summary>The version of the training sample file is not supported </summary>
	public const int Jl_ERR_TI_WRSMPVERS = 3806;

	/// <summary>At least one of the images is too small </summary>
	public const int Jl_ERR_TI_WRIMGSIZE = 3807;

	/// <summary>The samples do not match the current texture model </summary>
	public const int Jl_ERR_TI_WRSMPTEXMODEL = 3808;

	/// <summary>No images within the texture model </summary>
	public const int Jl_ERR_NOT_ENOUGH_IMAGES = 3809;

	/// <summary>The light source positions are linearly dependent </summary>
	public const int Jl_ERR_SING = 3850;

	/// <summary>No sufficient image indication </summary>
	public const int Jl_ERR_FEWIM = 3851;

	/// <summary>Internal error: Function has equal signs in JlZBrent </summary>
	public const int Jl_ERR_ZBR_NOS = 3852;

	/// <summary>Kalman: Dimension n,m or p has got a undefined value </summary>
	public const int Jl_ERR_DIMK = 3900;

	/// <summary>Kalman: File does not exist </summary>
	public const int Jl_ERR_NOFILE = 3901;

	/// <summary>Kalman: Error in file (row of dimension) </summary>
	public const int Jl_ERR_FF1 = 3902;

	/// <summary>Kalman: Error in file (row of marking) </summary>
	public const int Jl_ERR_FF2 = 3903;

	/// <summary>Error in file (value is no float) </summary>
	public const int Jl_ERR_FF3 = 3904;

	/// <summary>Kalman: Matrix A is missing in file </summary>
	public const int Jl_ERR_NO_A = 3905;

	/// <summary>Kalman: In Datei fehlt Matrix C </summary>
	public const int Jl_ERR_NO_C = 3906;

	/// <summary>Kalman: Matrix Q is missing in file </summary>
	public const int Jl_ERR_NO_Q = 3907;

	/// <summary>Kalman: Matrix R is missing in file </summary>
	public const int Jl_ERR_NO_R = 3908;

	/// <summary>Kalman: G or u is missing in file </summary>
	public const int Jl_ERR_NO_GU = 3909;

	/// <summary>Kalman: Covariant matrix is not symmetric </summary>
	public const int Jl_ERR_NOTSYMM = 3910;

	/// <summary>Kalman: Equation system is singular </summary>
	public const int Jl_ERR_SINGU = 3911;

	/// <summary>structured light model is not in persistent mode </summary>
	public const int Jl_ERR_SLM_NOT_PERSISTENT = 3950;

	/// <summary>the min_stripe_width is too large for the chosen pattern_width or pattern_height </summary>
	public const int Jl_ERR_SLM_MSW_TOO_LARGE = 3951;

	/// <summary>the single_stripe_width is too large for the chosen pattern_width or pattern_height </summary>
	public const int Jl_ERR_SLM_SSW_TOO_LARGE = 3952;

	/// <summary>min_stripe_width has to be smaller than single_stripe_width. </summary>
	public const int Jl_ERR_SLM_MSW_GT_SSW = 3953;

	/// <summary>single_stripe_width is too small for min_stripe_width. </summary>
	public const int Jl_ERR_SLM_SSW_LT_MSW = 3954;

	/// <summary>The SLM is not prepared for decoding. </summary>
	public const int Jl_ERR_SLM_NOT_PREP = 3955;

	/// <summary>The SLM does not contain the queried object. </summary>
	public const int Jl_ERR_SLM_NO_OBJS = 3956;

	/// <summary>The version of the structured light model is not supported </summary>
	public const int Jl_ERR_SLM_WRVERS = 3957;

	/// <summary>Invalid file format for a structured light model </summary>
	public const int Jl_ERR_SLM_WRFILE = 3958;

	/// <summary>Wrong pattern type</summary>
	public const int Jl_ERR_SLM_WRONGPATTERN = 3959;

	/// <summary>The SLM is not decoded for deflectometry. </summary>
	public const int Jl_ERR_SLM_NOT_DECODED = 3960;

	/// <summary>Wrong model type</summary>
	public const int Jl_ERR_SLM_WRONGMODEL = 3961;

	/// <summary>The csm has to contain two camera parameters </summary>
	public const int Jl_ERR_SLM_WNUMCAMS = 3962;

	/// <summary>Inconsistent projector size </summary>
	public const int Jl_ERR_SLM_WPATTSIZE = 3963;

	/// <summary>Camera type not supported </summary>
	public const int Jl_ERR_SLM_WRONGCTYPE = 3964;

	/// <summary>Projector type not supported </summary>
	public const int Jl_ERR_SLM_WRONGPTYPE = 3965;

	/// <summary>The SLM does not contain a csm </summary>
	public const int Jl_ERR_SLM_NO_CSM = 3966;

	/// <summary>The SLM is not set for vertical decoding </summary>
	public const int Jl_ERR_SLM_NO_VERT = 3967;

	/// <summary>The SLM is not decoded for reconstruction </summary>
	public const int Jl_ERR_SLM_NOT_DEC_REC = 3968;

	/// <summary>Inconsistent camera size </summary>
	public const int Jl_ERR_SLM_WCAMSIZE = 3969;

	/// <summary>Object is an object tuple </summary>
	public const int Jl_ERR_DBOIT = 4050;

	/// <summary>Object has been deleted already </summary>
	public const int Jl_ERR_DBOC = 4051;

	/// <summary>Wrong object-ID </summary>
	public const int Jl_ERR_DBWOID = 4052;

	/// <summary>Object tuple has been deleted already </summary>
	public const int Jl_ERR_DBTC = 4053;

	/// <summary>Wrong object tupel-ID </summary>
	public const int Jl_ERR_DBWTID = 4054;

	/// <summary>Object tuple is an object </summary>
	public const int Jl_ERR_DBTIO = 4055;

	/// <summary>Object-ID is NULL (0) </summary>
	public const int Jl_ERR_DBIDNULL = 4056;

	/// <summary>Object-ID outside the valid range </summary>
	public const int Jl_ERR_WDBID = 4057;

	/// <summary>Access to deleted image </summary>
	public const int Jl_ERR_DBIC = 4058;

	/// <summary>Access to image with wrong key </summary>
	public const int Jl_ERR_DBWIID = 4059;

	/// <summary>Access to deleted region </summary>
	public const int Jl_ERR_DBRC = 4060;

	/// <summary>Access to region with wrong key </summary>
	public const int Jl_ERR_DBWRID = 4061;

	/// <summary>Wrong value for image channel </summary>
	public const int Jl_ERR_WCHAN = 4062;

	/// <summary>Index too big </summary>
	public const int Jl_ERR_DBITL = 4063;

	/// <summary>Index not defined </summary>
	public const int Jl_ERR_DBIUNDEF = 4064;

	/// <summary>No OpenCL available </summary>
	public const int Jl_ERR_NO_OPENCL = 4100;

	/// <summary>OpenCL Error occurred </summary>
	public const int Jl_ERR_OPENCL_ERROR = 4101;

	/// <summary>No compute devices available </summary>
	public const int Jl_ERR_NO_COMPUTE_DEVICES = 4102;

	/// <summary>No device implementation for this parameter </summary>
	public const int Jl_ERR_NO_DEVICE_IMPL = 4103;

	/// <summary>Out of device memory </summary>
	public const int Jl_ERR_OUT_OF_DEVICE_MEM = 4104;

	/// <summary>Invalid work group shape </summary>
	public const int Jl_ERR_INVALID_SHAPE = 4105;

	/// <summary>Invalid compute device </summary>
	public const int Jl_ERR_INVALID_DEVICE = 4106;

	/// <summary>CUDA error occurred </summary>
	public const int Jl_ERR_CUDA_ERROR = 4200;

	/// <summary>cuDNN error occurred </summary>
	public const int Jl_ERR_CUDNN_ERROR = 4201;

	/// <summary>cuBLAS error occurred </summary>
	public const int Jl_ERR_CUBLAS_ERROR = 4202;

	/// <summary>Set batch_size not supported </summary>
	public const int Jl_ERR_BATCH_SIZE_NOT_SUPPORTED = 4203;

	/// <summary>CUDA implementations not available </summary>
	public const int Jl_ERR_CUDA_NOT_AVAILABLE = 4204;

	/// <summary>Unsupported version of cuDNN </summary>
	public const int Jl_ERR_CUDNN_UNSUPPORTED_VERSION = 4205;

	/// <summary>Requested feature not supported by cuDNN </summary>
	public const int Jl_ERR_CUDNN_FEATURE_NOT_SUPPORTED = 4206;

	/// <summary>CUDA driver is out-of-date </summary>
	public const int Jl_ERR_CUDA_DRIVER_VERSION = 4207;

	/// <summary>Training is unsupported with the selected runtime. </summary>
	public const int Jl_ERR_TRAINING_UNSUPPORTED = 4301;

	/// <summary>CPU based inference is not supported on this platform </summary>
	public const int Jl_ERR_CPU_INFERENCE_NOT_AVAILABLE = 4302;

	/// <summary>Error occurred in DNNL library </summary>
	public const int Jl_ERR_DNNL_ERROR = 4303;

	/// <summary>AI Accelerator Interface error occurred </summary>
	public const int Jl_ERR_HAI2_ERROR = 4320;

	/// <summary>Invalid parameter for AI Accelerator Interface </summary>
	public const int Jl_ERR_HAI2_INVALID_PARAM = 4321;

	/// <summary>ACL error occurred </summary>
	public const int Jl_ERR_ACL_ERROR = 4400;

	/// <summary>Internal visualization error </summary>
	public const int Jl_ERR_VISUALIZATION = 4500;

	/// <summary>Unexpected color type </summary>
	public const int Jl_ERR_COLOR_TYPE_UNEXP = 4501;

	/// <summary>Number of color settings exceeded </summary>
	public const int Jl_ERR_NUM_COLOR_EXCEEDED = 4502;

	/// <summary>Wrong (logical) window number </summary>
	public const int Jl_ERR_WSCN = 5100;

	/// <summary>Error while opening the window </summary>
	public const int Jl_ERR_DSCO = 5101;

	/// <summary>Wrong window coordinates </summary>
	public const int Jl_ERR_WWC = 5102;

	/// <summary>It is not possible to open another window </summary>
	public const int Jl_ERR_NWA = 5103;

	/// <summary>Device resp. operator not available </summary>
	public const int Jl_ERR_DNA = 5104;

	/// <summary>Unknown color </summary>
	public const int Jl_ERR_UCOL = 5105;

	/// <summary>No window has been opened for desired action </summary>
	public const int Jl_ERR_NWO = 5106;

	/// <summary>Wrong filling mode for regions </summary>
	public const int Jl_ERR_WFM = 5107;

	/// <summary>Wrong gray value (0..255) </summary>
	public const int Jl_ERR_WGV = 5108;

	/// <summary>Wrong pixel value </summary>
	public const int Jl_ERR_WPV = 5109;

	/// <summary>Wrong line width </summary>
	public const int Jl_ERR_WLW = 5110;

	/// <summary>Wrong name of cursor </summary>
	public const int Jl_ERR_WCUR = 5111;

	/// <summary>Wrong color table </summary>
	public const int Jl_ERR_WLUT = 5112;

	/// <summary>Wrong representation mode </summary>
	public const int Jl_ERR_WDM = 5113;

	/// <summary>Wrong representation color </summary>
	public const int Jl_ERR_WRCO = 5114;

	/// <summary>Wrong dither matrix </summary>
	public const int Jl_ERR_WRDM = 5115;

	/// <summary>Wrong image transformation </summary>
	public const int Jl_ERR_WRIT = 5116;

	/// <summary>Unsuitable image type for image trafo. </summary>
	public const int Jl_ERR_IPIT = 5117;

	/// <summary>Wrong zooming factor for image trafo. </summary>
	public const int Jl_ERR_WRZS = 5118;

	/// <summary>Wrong representation mode </summary>
	public const int Jl_ERR_WRDS = 5119;

	/// <summary>Wrong code of device </summary>
	public const int Jl_ERR_WRDV = 5120;

	/// <summary>Wrong number for father window </summary>
	public const int Jl_ERR_WWINF = 5121;

	/// <summary>Wrong window size </summary>
	public const int Jl_ERR_WDEXT = 5122;

	/// <summary>Wrong window type </summary>
	public const int Jl_ERR_WWT = 5123;

	/// <summary>No current window has been set </summary>
	public const int Jl_ERR_WND = 5124;

	/// <summary>Wrong color combination or range (RGB) </summary>
	public const int Jl_ERR_WRGB = 5125;

	/// <summary>Wrong number of pixels set </summary>
	public const int Jl_ERR_WPNS = 5126;

	/// <summary>Wrong value for comprise </summary>
	public const int Jl_ERR_WCM = 5127;

	/// <summary>set_fix with 1/4 image levels and static not valid </summary>
	public const int Jl_ERR_FNA = 5128;

	/// <summary>set_lut not valid in child windows </summary>
	public const int Jl_ERR_LNFS = 5129;

	/// <summary>Number of concurrent used color tables is too big </summary>
	public const int Jl_ERR_LOFL = 5130;

	/// <summary>Wrong device for window dump </summary>
	public const int Jl_ERR_WIDT = 5131;

	/// <summary>Wrong window size for window dump </summary>
	public const int Jl_ERR_WWDS = 5132;

	/// <summary>System variable DISPLAY not defined </summary>
	public const int Jl_ERR_NDVS = 5133;

	/// <summary>Wrong thickness for window margin </summary>
	public const int Jl_ERR_WBW = 5134;

	/// <summary>System variable DISPLAY has been set wrong (&lt;host&gt;:0.0) </summary>
	public const int Jl_ERR_WDVS = 5135;

	/// <summary>Too many fonts loaded </summary>
	public const int Jl_ERR_TMF = 5136;

	/// <summary>Wrong font name </summary>
	public const int Jl_ERR_WFN = 5137;

	/// <summary>No valid cursor position </summary>
	public const int Jl_ERR_WCP = 5138;

	/// <summary>Window is not a textual window </summary>
	public const int Jl_ERR_NTW = 5139;

	/// <summary>Window is not a image window </summary>
	public const int Jl_ERR_NPW = 5140;

	/// <summary>String too long or too high </summary>
	public const int Jl_ERR_STL = 5141;

	/// <summary>Too little space in the window rightw. </summary>
	public const int Jl_ERR_NSS = 5142;

	/// <summary>Window is not suitable for the mouse </summary>
	public const int Jl_ERR_NMS = 5143;

	/// <summary>Here Windows on a equal machine is permitted only </summary>
	public const int Jl_ERR_DWNA = 5144;

	/// <summary>Wrong mode while opening a window </summary>
	public const int Jl_ERR_WOM = 5145;

	/// <summary>Wrong window mode for operation </summary>
	public const int Jl_ERR_WWM = 5146;

	/// <summary>Operation not possible with fixed pixel </summary>
	public const int Jl_ERR_LUTF = 5147;

	/// <summary>Color tables for 8 image levels only </summary>
	public const int Jl_ERR_LUTN8 = 5148;

	/// <summary>Wrong mode for pseudo real colors </summary>
	public const int Jl_ERR_WTCM = 5149;

	/// <summary>Wrong pixel value for LUT </summary>
	public const int Jl_ERR_WIFTL = 5150;

	/// <summary>Wrong image size for pseudo real colors </summary>
	public const int Jl_ERR_WSOI = 5151;

	/// <summary>Error in procedure JlRLUT </summary>
	public const int Jl_ERR_HRLUT = 5152;

	/// <summary>Wrong number of entries in color table for set_lut </summary>
	public const int Jl_ERR_WPFSL = 5153;

	/// <summary>Wrong values for image area </summary>
	public const int Jl_ERR_WPVS = 5154;

	/// <summary>Wrong line pattern </summary>
	public const int Jl_ERR_WLPN = 5155;

	/// <summary>Wrong number of parameters for line pattern </summary>
	public const int Jl_ERR_WLPL = 5156;

	/// <summary>Wrong number of colors </summary>
	public const int Jl_ERR_WNOC = 5157;

	/// <summary>Wrong value for mode of area creation </summary>
	public const int Jl_ERR_WPST = 5158;

	/// <summary>Spy window is not set (set_spy) </summary>
	public const int Jl_ERR_SWNA = 5159;

	/// <summary>No file for spy has been set (set_spy) </summary>
	public const int Jl_ERR_NSFO = 5160;

	/// <summary>Wrong parameter output depth (set_spy) </summary>
	public const int Jl_ERR_WSPN = 5161;

	/// <summary>Wrong window size for window dump </summary>
	public const int Jl_ERR_WIFFD = 5162;

	/// <summary>Wrong color table: Wrong file name or query_lut() </summary>
	public const int Jl_ERR_WLUTF = 5163;

	/// <summary>Wrong color table: Empty string ? </summary>
	public const int Jl_ERR_WLUTE = 5164;

	/// <summary>Using this hardware set_lut('default') is allowed only </summary>
	public const int Jl_ERR_WLUTD = 5165;

	/// <summary>Error while calling online help </summary>
	public const int Jl_ERR_CNDP = 5166;

	/// <summary>Row can not be projected </summary>
	public const int Jl_ERR_LNPR = 5167;

	/// <summary>Operation is unsuitable using a computer with fixed color table </summary>
	public const int Jl_ERR_NFSC = 5168;

	/// <summary>Computer represents gray scales only </summary>
	public const int Jl_ERR_NACD = 5169;

	/// <summary>LUT of this display is full </summary>
	public const int Jl_ERR_LUTO = 5170;

	/// <summary>Internal error: wrong color code </summary>
	public const int Jl_ERR_WCC = 5171;

	/// <summary>Wrong type for window attribute </summary>
	public const int Jl_ERR_WWATTRT = 5172;

	/// <summary>Wrong name for window attribute </summary>
	public const int Jl_ERR_WWATTRN = 5173;

	/// <summary>negative height of area (or 0) </summary>
	public const int Jl_ERR_WRSPART = 5174;

	/// <summary>negative width of area (or 0) </summary>
	public const int Jl_ERR_WCSPART = 5175;

	/// <summary>Window not completely visible </summary>
	public const int Jl_ERR_WNCV = 5176;

	/// <summary>Font not allowed for this operation </summary>
	public const int Jl_ERR_FONT_NA = 5177;

	/// <summary>Window was created in different thread </summary>
	public const int Jl_ERR_WDIFFTH = 5178;

	/// <summary>Drawing object already attached to another window </summary>
	public const int Jl_ERR_OBJ_ATTACHED = 5194;

	/// <summary>Internal error: only RGB-Mode </summary>
	public const int Jl_ERR_CHA3 = 5180;

	/// <summary>No more (image-)windows available </summary>
	public const int Jl_ERR_NMWA = 5181;

	/// <summary>Depth was not stored with window </summary>
	public const int Jl_ERR_DEPTH_NOT_STORED = 5179;

	/// <summary>Object index was not stored with window </summary>
	public const int Jl_ERR_INDEX_NOT_STORED = 5182;

	/// <summary>Operator does not support primitives without point coordinates </summary>
	public const int Jl_ERR_PRIM_NO_POINTS = 5183;

	/// <summary>Maximum image size for Windows Remote Desktop exceeded </summary>
	public const int Jl_ERR_REMOTE_DESKTOP_SIZE = 5184;

	/// <summary>No OpenGL support available </summary>
	public const int Jl_ERR_NOGL = 5185;

	/// <summary>No depth information available </summary>
	public const int Jl_ERR_NODEPTH = 5186;

	/// <summary>OpenGL error </summary>
	public const int Jl_ERR_OGL_ERROR = 5187;

	/// <summary>Required framebuffer object is unsupported </summary>
	public const int Jl_ERR_UNSUPPORTED_FBO = 5188;

	/// <summary>OpenGL accelerated hidden surface removal not supported on this machine </summary>
	public const int Jl_ERR_OGL_HSR_NOT_SUPPORTED = 5189;

	/// <summary>Invalid window parameter </summary>
	public const int Jl_ERR_WP_IWP = 5190;

	/// <summary>Invalid value for window parameter </summary>
	public const int Jl_ERR_WP_IWPV = 5191;

	/// <summary>Unknown mode </summary>
	public const int Jl_ERR_UMOD = 5192;

	/// <summary>No image attached </summary>
	public const int Jl_ERR_ATTIMG = 5193;

	/// <summary>invalid navigation mode </summary>
	public const int Jl_ERR_NVG_WM = 5195;

	/// <summary>Internal file error </summary>
	public const int Jl_ERR_FINTERN = 5196;

	/// <summary>Error while file synchronisation </summary>
	public const int Jl_ERR_FS = 5197;

	/// <summary>insufficient rights </summary>
	public const int Jl_ERR_FISR = 5198;

	/// <summary>Bad file descriptor </summary>
	public const int Jl_ERR_BFD = 5199;

	/// <summary>File not found </summary>
	public const int Jl_ERR_FNF = 5200;

	/// <summary>Error while writing image data (sufficient memory ?) </summary>
	public const int Jl_ERR_DWI = 5201;

	/// <summary>Error while writing image descriptor (sufficient memory ?) </summary>
	public const int Jl_ERR_DWID = 5202;

	/// <summary>Error while reading image data (format of image too small ?) </summary>
	public const int Jl_ERR_DRI1 = 5203;

	/// <summary>Error while reading image data (format of image too big ?) </summary>
	public const int Jl_ERR_DRI2 = 5204;

	/// <summary>Error while reading image descriptor: File too small </summary>
	public const int Jl_ERR_DRID1 = 5205;

	/// <summary>Image matrices are different </summary>
	public const int Jl_ERR_DIMMAT = 5206;

	/// <summary>Help file not found (setenv VisionROOT) </summary>
	public const int Jl_ERR_HNF = 5207;

	/// <summary>Help index not found (setenv VisionROOT) </summary>
	public const int Jl_ERR_XNF = 5208;

	/// <summary>File &lt;standard_input&gt; can not be closed </summary>
	public const int Jl_ERR_CNCSI = 5209;

	/// <summary>&lt;standard_output/error&gt; can not be closed </summary>
	public const int Jl_ERR_CNCSO = 5210;

	/// <summary>File can not be closed </summary>
	public const int Jl_ERR_CNCF = 5211;

	/// <summary>Error while writing to file </summary>
	public const int Jl_ERR_EDWF = 5212;

	/// <summary>Exceeding of maximum number of files </summary>
	public const int Jl_ERR_NFA = 5213;

	/// <summary>Wrong file name </summary>
	public const int Jl_ERR_WFIN = 5214;

	/// <summary>Error while opening the file </summary>
	public const int Jl_ERR_CNOF = 5215;

	/// <summary>Wrong file mode </summary>
	public const int Jl_ERR_WFMO = 5216;

	/// <summary>Wrong type for pixel (e.g. byte) </summary>
	public const int Jl_ERR_WPTY = 5217;

	/// <summary>Wrong image width (too big ?) </summary>
	public const int Jl_ERR_WIW = 5218;

	/// <summary>Wrong image height (too big ?) </summary>
	public const int Jl_ERR_WIH = 5219;

	/// <summary>File already exhausted before reading an image </summary>
	public const int Jl_ERR_FTS1 = 5220;

	/// <summary>File exhausted before terminating the image </summary>
	public const int Jl_ERR_FTS2 = 5221;

	/// <summary>Wrong value for resolution (dpi) </summary>
	public const int Jl_ERR_WDPI = 5222;

	/// <summary>Wrong output image size (width) </summary>
	public const int Jl_ERR_WNOW = 5223;

	/// <summary>Wrong output image size (height) </summary>
	public const int Jl_ERR_WNOH = 5224;

	/// <summary>Wrong number of parameter values: Format description </summary>
	public const int Jl_ERR_WNFP = 5225;

	/// <summary>Wrong parameter name for operator </summary>
	public const int Jl_ERR_WPNA = 5226;

	/// <summary>Wrong slot name for parameter </summary>
	public const int Jl_ERR_WSNA = 5227;

	/// <summary>Operator class is missing in help file </summary>
	public const int Jl_ERR_NPCF = 5228;

	/// <summary>Wrong or inconsistent help/ *.idx or help/ *.sta </summary>
	public const int Jl_ERR_WHIF = 5229;

	/// <summary>File help/ *.idx not found </summary>
	public const int Jl_ERR_HINF = 5230;

	/// <summary>File help/ *.sta not found </summary>
	public const int Jl_ERR_HSNF = 5231;

	/// <summary>Inconsistent file help/ *.sta </summary>
	public const int Jl_ERR_ICSF = 5232;

	/// <summary>No explication file (.exp) found </summary>
	public const int Jl_ERR_EFNF = 5233;

	/// <summary>No file found in known graphic format </summary>
	public const int Jl_ERR_NFWKEF = 5234;

	/// <summary>Wrong graphic format </summary>
	public const int Jl_ERR_WIFT = 5235;

	/// <summary>Inconsistent file Vision.num </summary>
	public const int Jl_ERR_ICNF = 5236;

	/// <summary>File with extension 'tiff' is no Tiff-file </summary>
	public const int Jl_ERR_WTIFF = 5237;

	/// <summary>Wrong file format </summary>
	public const int Jl_ERR_WFF = 5238;

	/// <summary>No PNM format </summary>
	public const int Jl_ERR_NOPNM = 5242;

	/// <summary>Inconsistent or old help file </summary>
	public const int Jl_ERR_ICODB = 5243;

	/// <summary>Invalid file encoding </summary>
	public const int Jl_ERR_INVAL_FILE_ENC = 5244;

	/// <summary>File not open </summary>
	public const int Jl_ERR_FNO = 5245;

	/// <summary>No files in use so far (none opened) </summary>
	public const int Jl_ERR_NO_FILES = 5246;

	/// <summary>Invalid file format for regions </summary>
	public const int Jl_ERR_NORFILE = 5247;

	/// <summary>Error while reading region data: Format of region too big. </summary>
	public const int Jl_ERR_RDTB = 5248;

	/// <summary>Encoding for binary files not allowed </summary>
	public const int Jl_ERR_BINFILE_ENC = 5249;

	/// <summary>Error reading file </summary>
	public const int Jl_ERR_EDRF = 5250;

	/// <summary>Serial port not open </summary>
	public const int Jl_ERR_SNO = 5251;

	/// <summary>No serial port available </summary>
	public const int Jl_ERR_NSA = 5252;

	/// <summary>Could not open serial port </summary>
	public const int Jl_ERR_CNOS = 5253;

	/// <summary>Could not close serial port </summary>
	public const int Jl_ERR_CNCS = 5254;

	/// <summary>Could not get serial port attributes </summary>
	public const int Jl_ERR_CNGSA = 5255;

	/// <summary>Could not set serial port attributes </summary>
	public const int Jl_ERR_CNSSA = 5256;

	/// <summary>Wrong baud rate for serial connection </summary>
	public const int Jl_ERR_WRSBR = 5257;

	/// <summary>Wrong number of data bits for serial connection </summary>
	public const int Jl_ERR_WRSDB = 5258;

	/// <summary>Wrong flow control for serial connection </summary>
	public const int Jl_ERR_WRSFC = 5259;

	/// <summary>Could not flush serial port </summary>
	public const int Jl_ERR_CNFS = 5260;

	/// <summary>Error during write to serial port </summary>
	public const int Jl_ERR_EDWS = 5261;

	/// <summary>Error during read from serial port </summary>
	public const int Jl_ERR_EDRS = 5262;

	/// <summary>Serialized item does not contain valid regions. </summary>
	public const int Jl_ERR_REG_NOSITEM = 5270;

	/// <summary>The version of the regions is not supported. </summary>
	public const int Jl_ERR_REG_WRVERS = 5271;

	/// <summary>Serialized item does not contain valid images. </summary>
	public const int Jl_ERR_IMG_NOSITEM = 5272;

	/// <summary>The version of the images is not supported. </summary>
	public const int Jl_ERR_IMG_WRVERS = 5273;

	/// <summary>Serialized item does not contain valid XLD objects. </summary>
	public const int Jl_ERR_XLD_NOSITEM = 5274;

	/// <summary>The version of the XLD objects is not supported. </summary>
	public const int Jl_ERR_XLD_WRVERS = 5275;

	/// <summary>Serialized item does not contain valid objects. </summary>
	public const int Jl_ERR_OBJ_NOSITEM = 5276;

	/// <summary>The version of the objects is not supported. </summary>
	public const int Jl_ERR_OBJ_WRVERS = 5277;

	/// <summary>XLD object data can only be read by Vision XL </summary>
	public const int Jl_ERR_XLD_DATA_TOO_LARGE = 5678;

	/// <summary>Unexpected object detected </summary>
	public const int Jl_ERR_OBJ_UNEXPECTED = 5279;

	/// <summary>File has not been opened in text format </summary>
	public const int Jl_ERR_FNOTF = 5280;

	/// <summary>File has not been opened in binary file format </summary>
	public const int Jl_ERR_FNOBF = 5281;

	/// <summary>Cannot create directory </summary>
	public const int Jl_ERR_DIRCR = 5282;

	/// <summary>Cannot remove directory </summary>
	public const int Jl_ERR_DIRRM = 5283;

	/// <summary>Cannot get current directory </summary>
	public const int Jl_ERR_GETCWD = 5284;

	/// <summary>Cannot set current directory </summary>
	public const int Jl_ERR_SETCWD = 5285;

	/// <summary>Need to call XInitThreads() </summary>
	public const int Jl_ERR_XINIT = 5286;

	/// <summary>No image acquisition device opened </summary>
	public const int Jl_ERR_NFS = 5300;

	/// <summary>IA: wrong color depth </summary>
	public const int Jl_ERR_FGWC = 5301;

	/// <summary>IA: wrong device </summary>
	public const int Jl_ERR_FGWD = 5302;

	/// <summary>IA: determination of video format not possible </summary>
	public const int Jl_ERR_FGVF = 5303;

	/// <summary>IA: no video signal </summary>
	public const int Jl_ERR_FGNV = 5304;

	/// <summary>Unknown image acquisition device </summary>
	public const int Jl_ERR_UFG = 5305;

	/// <summary>IA: failed grabbing of an image </summary>
	public const int Jl_ERR_FGF = 5306;

	/// <summary>IA: wrong resolution chosen </summary>
	public const int Jl_ERR_FGWR = 5307;

	/// <summary>IA: wrong image part chosen </summary>
	public const int Jl_ERR_FGWP = 5308;

	/// <summary>IA: wrong pixel ratio chosen </summary>
	public const int Jl_ERR_FGWPR = 5309;

	/// <summary>IA: handle not valid </summary>
	public const int Jl_ERR_FGWH = 5310;

	/// <summary>IA: instance not valid (already closed?) </summary>
	public const int Jl_ERR_FGCL = 5311;

	/// <summary>Image acquisition device could not be initialized </summary>
	public const int Jl_ERR_FGNI = 5312;

	/// <summary>IA: external triggering not supported </summary>
	public const int Jl_ERR_FGET = 5313;

	/// <summary>IA: wrong camera input line (multiplex) </summary>
	public const int Jl_ERR_FGLI = 5314;

	/// <summary>IA: wrong color space </summary>
	public const int Jl_ERR_FGCS = 5315;

	/// <summary>IA: wrong port </summary>
	public const int Jl_ERR_FGPT = 5316;

	/// <summary>IA: wrong camera type </summary>
	public const int Jl_ERR_FGCT = 5317;

	/// <summary>IA: maximum number of acquisition device classes exceeded </summary>
	public const int Jl_ERR_FGTM = 5318;

	/// <summary>IA: device busy </summary>
	public const int Jl_ERR_FGDV = 5319;

	/// <summary>IA: asynchronous grab not supported </summary>
	public const int Jl_ERR_FGASYNC = 5320;

	/// <summary>IA: unsupported parameter </summary>
	public const int Jl_ERR_FGPARAM = 5321;

	/// <summary>IA: timeout </summary>
	public const int Jl_ERR_FGTIMEOUT = 5322;

	/// <summary>IA: invalid gain </summary>
	public const int Jl_ERR_FGGAIN = 5323;

	/// <summary>IA: invalid field </summary>
	public const int Jl_ERR_FGFIELD = 5324;

	/// <summary>IA: invalid parameter type </summary>
	public const int Jl_ERR_FGPART = 5325;

	/// <summary>IA: invalid parameter value </summary>
	public const int Jl_ERR_FGPARV = 5326;

	/// <summary>IA: function not supported </summary>
	public const int Jl_ERR_FGFNS = 5327;

	/// <summary>IA: incompatible interface version </summary>
	public const int Jl_ERR_FGIVERS = 5328;

	/// <summary>IA: could not set parameter value </summary>
	public const int Jl_ERR_FGSETPAR = 5329;

	/// <summary>IA: could not query parameter setting </summary>
	public const int Jl_ERR_FGGETPAR = 5330;

	/// <summary>IA: parameter not available in current configuration </summary>
	public const int Jl_ERR_FGPARNA = 5331;

	/// <summary>IA: device could not be closed properly </summary>
	public const int Jl_ERR_FGCLOSE = 5332;

	/// <summary>IA: camera configuration file could not be opened </summary>
	public const int Jl_ERR_FGCAMFILE = 5333;

	/// <summary>IA: unsupported callback type </summary>
	public const int Jl_ERR_FGCALLBACK = 5334;

	/// <summary>IA: device lost </summary>
	public const int Jl_ERR_FGDEVLOST = 5335;

	/// <summary>IA: grab aborted </summary>
	public const int Jl_ERR_FGABORTED = 5336;

	/// <summary>IO: timeout </summary>
	public const int Jl_ERR_IOTIMEOUT = 5350;

	/// <summary>IO: incompatible interface version </summary>
	public const int Jl_ERR_IOIVERS = 5351;

	/// <summary>IO: handle not valid </summary>
	public const int Jl_ERR_IOWH = 5352;

	/// <summary>IO: device busy </summary>
	public const int Jl_ERR_IODBUSY = 5353;

	/// <summary>IO: insufficient user rights </summary>
	public const int Jl_ERR_IOIAR = 5354;

	/// <summary>IO: device or channel not found </summary>
	public const int Jl_ERR_IONF = 5355;

	/// <summary>IO: invalid parameter type </summary>
	public const int Jl_ERR_IOPART = 5356;

	/// <summary>IO: invalid parameter value </summary>
	public const int Jl_ERR_IOPARV = 5357;

	/// <summary>IO: invalid parameter number </summary>
	public const int Jl_ERR_IOPARNUM = 5358;

	/// <summary>IO: unsupported parameter </summary>
	public const int Jl_ERR_IOPARAM = 5359;

	/// <summary>IO: parameter not available in curr config.</summary>
	public const int Jl_ERR_IOPARNA = 5360;

	/// <summary>IO: function not supported </summary>
	public const int Jl_ERR_IOFNS = 5361;

	/// <summary>IO: maximum number of dio classes exceeded</summary>
	public const int Jl_ERR_IOME = 5362;

	/// <summary>IO: driver of io device not available </summary>
	public const int Jl_ERR_IODNA = 5363;

	/// <summary>IO: operation aborted </summary>
	public const int Jl_ERR_IOABORTED = 5364;

	/// <summary>IO: invalid data type </summary>
	public const int Jl_ERR_IODATT = 5365;

	/// <summary>IO: device lost </summary>
	public const int Jl_ERR_IODEVLOST = 5366;

	/// <summary>IO: could not set parameter value </summary>
	public const int Jl_ERR_IOSETPAR = 5367;

	/// <summary>IO: could not query parameter setting </summary>
	public const int Jl_ERR_IOGETPAR = 5368;

	/// <summary>IO: device could not be closed properly </summary>
	public const int Jl_ERR_IOCLOSE = 5369;

	/// <summary>Image type is not supported </summary>
	public const int Jl_ERR_JXR_UNSUPPORTED_FORMAT = 5400;

	/// <summary>Invalid pixel format passed to filter function </summary>
	public const int Jl_ERR_JXR_INVALID_PIXEL_FORMAT = 5401;

	/// <summary>Internal JpegXR error. </summary>
	public const int Jl_ERR_JXR_INTERNAL_ERROR = 5402;

	/// <summary>Syntax error in output format string </summary>
	public const int Jl_ERR_JXR_FORMAT_SYNTAX_ERROR = 5403;

	/// <summary>Maximum number of channels exceeded </summary>
	public const int Jl_ERR_JXR_TOO_MANY_CHANNELS = 5404;

	/// <summary>Unspecified error in JXR library </summary>
	public const int Jl_ERR_JXR_EC_ERROR = 5405;

	/// <summary>Bad magic number in JXR library </summary>
	public const int Jl_ERR_JXR_EC_BADMAGIC = 5406;

	/// <summary>Feature not implemented in JXR library </summary>
	public const int Jl_ERR_JXR_EC_FEATURE_NOT_IMPLEMENTED = 5407;

	/// <summary>File read/write error in JXR library </summary>
	public const int Jl_ERR_JXR_EC_IO = 5408;

	/// <summary>Bad file format in JXR library </summary>
	public const int Jl_ERR_JXR_EC_BADFORMAT = 5409;

	/// <summary>Error while closing the image file </summary>
	public const int Jl_ERR_LIB_FILE_CLOSE = 5500;

	/// <summary>Error while opening the image file </summary>
	public const int Jl_ERR_LIB_FILE_OPEN = 5501;

	/// <summary>Premature end of the image file </summary>
	public const int Jl_ERR_LIB_UNEXPECTED_EOF = 5502;

	/// <summary>Image dimensions too large for this file format </summary>
	public const int Jl_ERR_IDTL = 5503;

	/// <summary>Image too large for this Vision version </summary>
	public const int Jl_ERR_ITLHV = 5504;

	/// <summary>Too many iconic objects for this file format </summary>
	public const int Jl_ERR_TMIO = 5505;

	/// <summary>File format is unsupported </summary>
	public const int Jl_ERR_FILE_FORMAT_UNSUPPORTED = 5506;

	/// <summary>All channels must have equal dimensions </summary>
	public const int Jl_ERR_INCONSISTENT_DIMENSIONS = 5507;

	/// <summary>Bad file format specification </summary>
	public const int Jl_ERR_FILE_BAD_SPECIFICATION = 5508;

	/// <summary>File is no PCX-File </summary>
	public const int Jl_ERR_PCX_NO_PCX_FILE = 5510;

	/// <summary>Unknown encoding </summary>
	public const int Jl_ERR_PCX_UNKNOWN_ENCODING = 5511;

	/// <summary>More than 4 image plains </summary>
	public const int Jl_ERR_PCX_MORE_THAN_4_PLANES = 5512;

	/// <summary>Wrong magic in color table </summary>
	public const int Jl_ERR_PCX_COLORMAP_SIGNATURE = 5513;

	/// <summary>Wrong number of bytes in span </summary>
	public const int Jl_ERR_PCX_REPEAT_COUNT_SPANS = 5514;

	/// <summary>Wrong number of bits/pixels </summary>
	public const int Jl_ERR_PCX_TOO_MUCH_BITS_PIXEL = 5515;

	/// <summary>Wrong number of plains </summary>
	public const int Jl_ERR_PCX_PACKED_PIXELS = 5516;

	/// <summary>File is no GIF-File </summary>
	public const int Jl_ERR_GIF_NO_GIF_PICTURE = 5520;

	/// <summary>GIF: Wrong version </summary>
	public const int Jl_ERR_GIF_BAD_VERSION = 5521;

	/// <summary>GIF: Wrong descriptor </summary>
	public const int Jl_ERR_GIF_SCREEN_DESCRIPTOR = 5522;

	/// <summary>GIF: Wrong color table </summary>
	public const int Jl_ERR_GIF_COLORMAP = 5523;

	/// <summary>GIF: Premature end of file </summary>
	public const int Jl_ERR_GIF_READ_ERROR_EOF = 5524;

	/// <summary>GIF: Wrong number of images </summary>
	public const int Jl_ERR_GIF_NOT_ENOUGH_IMAGES = 5525;

	/// <summary>GIF: Wrong image extension </summary>
	public const int Jl_ERR_GIF_ERROR_ON_EXTENSION = 5526;

	/// <summary>GIF: Wrong left top width </summary>
	public const int Jl_ERR_GIF_LEFT_TOP_WIDTH = 5527;

	/// <summary>GIF: Cyclic index of table </summary>
	public const int Jl_ERR_GIF_CIRCULAR_TABL_ENTRY = 5528;

	/// <summary>GIF: Wrong image data </summary>
	public const int Jl_ERR_GIF_BAD_IMAGE_DATA = 5529;

	/// <summary>File is no Sun-Raster-File </summary>
	public const int Jl_ERR_SUN_RASTERFILE_TYPE = 5530;

	/// <summary>Wrong header </summary>
	public const int Jl_ERR_SUN_RASTERFILE_HEADER = 5531;

	/// <summary>Wrong image width </summary>
	public const int Jl_ERR_SUN_COLS = 5532;

	/// <summary>Wrong image height </summary>
	public const int Jl_ERR_SUN_ROWS = 5533;

	/// <summary>Wrong color map </summary>
	public const int Jl_ERR_SUN_COLORMAP = 5534;

	/// <summary>Wrong image data </summary>
	public const int Jl_ERR_SUN_RASTERFILE_IMAGE = 5535;

	/// <summary>Wrong type of pixel </summary>
	public const int Jl_ERR_SUN_IMPOSSIBLE_DATA = 5536;

	/// <summary>Wrong type of pixel </summary>
	public const int Jl_ERR_XWD_IMPOSSIBLE_DATA = 5540;

	/// <summary>Wrong visual class </summary>
	public const int Jl_ERR_XWD_VISUAL_CLASS = 5541;

	/// <summary>Wrong X10 header </summary>
	public const int Jl_ERR_XWD_X10_HEADER = 5542;

	/// <summary>Wrong X11 header </summary>
	public const int Jl_ERR_XWD_X11_HEADER = 5543;

	/// <summary>Wrong X10 colormap </summary>
	public const int Jl_ERR_XWD_X10_COLORMAP = 5544;

	/// <summary>Wrong X11 colormap </summary>
	public const int Jl_ERR_XWD_X11_COLORMAP = 5545;

	/// <summary>Wrong pixmap </summary>
	public const int Jl_ERR_XWD_X11_PIXMAP = 5546;

	/// <summary>Unknown version </summary>
	public const int Jl_ERR_XWD_UNKNOWN_VERSION = 5547;

	/// <summary>Error while reading an image </summary>
	public const int Jl_ERR_XWD_READING_IMAGE = 5548;

	/// <summary>Error while reading a file </summary>
	public const int Jl_ERR_TIF_BAD_INPUTDATA = 5550;

	/// <summary>Wrong colormap </summary>
	public const int Jl_ERR_TIF_COLORMAP = 5551;

	/// <summary>Too many colors </summary>
	public const int Jl_ERR_TIF_TOO_MANY_COLORS = 5552;

	/// <summary>Wrong photometric interpretation</summary>
	public const int Jl_ERR_TIF_BAD_PHOTOMETRIC = 5553;

	/// <summary>Wrong photometric depth </summary>
	public const int Jl_ERR_TIF_PHOTOMETRIC_DEPTH = 5554;

	/// <summary>Image is no binary file </summary>
	public const int Jl_ERR_TIF_NO_REGION = 5555;

	/// <summary>Unsupported TIFF format </summary>
	public const int Jl_ERR_TIF_UNSUPPORTED_FORMAT = 5556;

	/// <summary>Wrong file format specification </summary>
	public const int Jl_ERR_TIF_BAD_SPECIFICATION = 5557;

	/// <summary>TIFF file is corrupt </summary>
	public const int Jl_ERR_TIF_FILE_CORRUPT = 5558;

	/// <summary>Required TIFF tag is missing </summary>
	public const int Jl_ERR_TIF_TAG_UNDEFINED = 5559;

	/// <summary>File is no BMP-File </summary>
	public const int Jl_ERR_BMP_NO_BMP_PICTURE = 5560;

	/// <summary>Premature end of file </summary>
	public const int Jl_ERR_BMP_READ_ERROR_EOF = 5561;

	/// <summary>Incomplete header </summary>
	public const int Jl_ERR_BMP_INCOMPLETE_HEADER = 5562;

	/// <summary>Unknown bitmap format </summary>
	public const int Jl_ERR_BMP_UNKNOWN_FORMAT = 5563;

	/// <summary>Unknown compression format </summary>
	public const int Jl_ERR_BMP_UNKNOWN_COMPRESSION = 5564;

	/// <summary>Wrong color table </summary>
	public const int Jl_ERR_BMP_COLORMAP = 5565;

	/// <summary>Write error on output </summary>
	public const int Jl_ERR_BMP_WRITE_ERROR = 5566;

	/// <summary>File does not contain a binary image </summary>
	public const int Jl_ERR_BMP_NO_REGION = 5567;

	/// <summary>Wrong number of components in image </summary>
	public const int Jl_ERR_JPG_COMP_NUM = 5570;

	/// <summary>Unknown error from libjpeg </summary>
	public const int Jl_ERR_JPGLIB_UNKNOWN = 5571;

	/// <summary>Not implemented feature in libjpeg </summary>
	public const int Jl_ERR_JPGLIB_NOTIMPL = 5572;

	/// <summary>File access error in libjpeg </summary>
	public const int Jl_ERR_JPGLIB_FILE = 5573;

	/// <summary>Tmp file access error in libjpeg </summary>
	public const int Jl_ERR_JPGLIB_TMPFILE = 5574;

	/// <summary>Memory error in libjpeg </summary>
	public const int Jl_ERR_JPGLIB_MEMORY = 5575;

	/// <summary>Error in input image </summary>
	public const int Jl_ERR_JPGLIB_INFORMAT = 5576;

	/// <summary>File is not a PNG file </summary>
	public const int Jl_ERR_PNG_NO_PNG_FILE = 5580;

	/// <summary>Unknown interlace type </summary>
	public const int Jl_ERR_PNG_UNKNOWN_INTERLACE_TYPE = 5581;

	/// <summary>Unsupported color type </summary>
	public const int Jl_ERR_PNG_UNSUPPORTED_COLOR_TYPE = 5582;

	/// <summary>Image is no binary file </summary>
	public const int Jl_ERR_PNG_NO_REGION = 5583;

	/// <summary>Image size too big </summary>
	public const int Jl_ERR_PNG_SIZE_TOO_BIG = 5584;

	/// <summary>Error accessing TIFF tag </summary>
	public const int Jl_ERR_TIF_TAG_ACCESS = 5587;

	/// <summary>Invalid TIFF tag value datatype </summary>
	public const int Jl_ERR_TIF_TAG_DATATYPE = 5588;

	/// <summary>Unsupported TIFF tag requested </summary>
	public const int Jl_ERR_TIF_TAG_UNSUPPORTED = 5589;

	/// <summary>File corrupt </summary>
	public const int Jl_ERR_JP2_CORRUPT = 5590;

	/// <summary>Image precision too high </summary>
	public const int Jl_ERR_JP2_PREC_TOO_HIGH = 5591;

	/// <summary>Error while encoding </summary>
	public const int Jl_ERR_JP2_ENCODING_ERROR = 5592;

	/// <summary>Image size too big </summary>
	public const int Jl_ERR_JP2_SIZE_TOO_BIG = 5593;

	/// <summary>Unknown internal error from OpenJPEG </summary>
	public const int Jl_ERR_JP2_INTERNAL_ERROR = 5594;

	/// <summary>File does not contain only images </summary>
	public const int Jl_ERR_HOBJ_NOT_ONLY_IMAGES = 5599;

	/// <summary>Socket can not be set to block </summary>
	public const int Jl_ERR_SOCKET_BLOCK = 5600;

	/// <summary>Socket can not be set to unblock </summary>
	public const int Jl_ERR_SOCKET_UNBLOCK = 5601;

	/// <summary>Received data is no tuple </summary>
	public const int Jl_ERR_SOCKET_NO_CPAR = 5602;

	/// <summary>Received data is no image </summary>
	public const int Jl_ERR_SOCKET_NO_IMAGE = 5603;

	/// <summary>Received data is no region </summary>
	public const int Jl_ERR_SOCKET_NO_RL = 5604;

	/// <summary>Received data is no xld object </summary>
	public const int Jl_ERR_SOCKET_NO_XLD = 5605;

	/// <summary>Error while reading from socket </summary>
	public const int Jl_ERR_SOCKET_READ_DATA_FAILED = 5606;

	/// <summary>Error while writing to socket </summary>
	public const int Jl_ERR_SOCKET_WRITE_DATA_FAILED = 5607;

	/// <summary>Illegal number of bytes with get_rl </summary>
	public const int Jl_ERR_SOCKET_WRONG_BYTE_NUMBER = 5608;

	/// <summary>Buffer overflow in read_data </summary>
	public const int Jl_ERR_SOCKET_BUFFER_OVERFLOW = 5609;

	/// <summary>Socket can not be created </summary>
	public const int Jl_ERR_SOCKET_CANT_ASSIGN_FD = 5610;

	/// <summary>Bind on socket failed </summary>
	public const int Jl_ERR_SOCKET_CANT_BIND = 5611;

	/// <summary>Socket information is not available </summary>
	public const int Jl_ERR_SOCKET_CANT_GET_PORTNUMBER = 5612;

	/// <summary>Socket cannot listen for incoming connections </summary>
	public const int Jl_ERR_SOCKET_CANT_LISTEN = 5613;

	/// <summary>Connection could not be accepted </summary>
	public const int Jl_ERR_SOCKET_CANT_ACCEPT = 5614;

	/// <summary>Connection request failed </summary>
	public const int Jl_ERR_SOCKET_CANT_CONNECT = 5615;

	/// <summary>Hostname could not be resolved </summary>
	public const int Jl_ERR_SOCKET_GETHOSTBYNAME = 5616;

	/// <summary>Unknown tuple type on socket </summary>
	public const int Jl_ERR_SOCKET_ILLEGAL_TUPLE_TYPE = 5618;

	/// <summary>Timeout occurred on socket </summary>
	public const int Jl_ERR_SOCKET_TIMEOUT = 5619;

	/// <summary>No more sockets available </summary>
	public const int Jl_ERR_SOCKET_NA = 5620;

	/// <summary>Socket is not initialized </summary>
	public const int Jl_ERR_SOCKET_NI = 5621;

	/// <summary>Invalid socket </summary>
	public const int Jl_ERR_SOCKET_OOR = 5622;

	/// <summary>Socket is NULL </summary>
	public const int Jl_ERR_SOCKET_IS = 5623;

	/// <summary>Received data type is too large </summary>
	public const int Jl_ERR_SOCKET_DATA_TOO_LARGE = 5624;

	/// <summary>Wrong socket type. </summary>
	public const int Jl_ERR_SOCKET_WRONG_TYPE = 5625;

	/// <summary>Received data is not packed. </summary>
	public const int Jl_ERR_SOCKET_NO_PACKED_DATA = 5626;

	/// <summary>Socket parameter operation failed. </summary>
	public const int Jl_ERR_SOCKET_PARAM_FAILED = 5627;

	/// <summary>The data does not match the format specification. </summary>
	public const int Jl_ERR_SOCKET_FORMAT_MISMATCH = 5628;

	/// <summary>Invalid format specification. </summary>
	public const int Jl_ERR_SOCKET_INVALID_FORMAT = 5629;

	/// <summary>Received data is no serialized item </summary>
	public const int Jl_ERR_SOCKET_NO_SERIALIZED_ITEM = 5630;

	/// <summary>Unable to create SSL context </summary>
	public const int Jl_ERR_SOCKET_TLS_CONTEXT = 5631;

	/// <summary>Invalid TLS certificate or private key </summary>
	public const int Jl_ERR_SOCKET_TLS_CERT_KEY = 5632;

	/// <summary>Invalid TLS private key </summary>
	public const int Jl_ERR_SOCKET_TLS_HANDSHAKE = 5633;

	/// <summary>Too many contours/polygons for this file format </summary>
	public const int Jl_ERR_ARCINFO_TOO_MANY_XLDS = 5700;

	/// <summary>The version of the quaternion is not supported </summary>
	public const int Jl_ERR_QUAT_WRONG_VERSION = 5750;

	/// <summary>Serialized item does not contain a valid quaternion</summary>
	public const int Jl_ERR_QUAT_NOSITEM = 5751;

	/// <summary>The version of the homogeneous matrix is not supported </summary>
	public const int Jl_ERR_HOM_MAT2D_WRONG_VERSION = 5752;

	/// <summary>Serialized item does not contain a valid homogeneous matrix </summary>
	public const int Jl_ERR_HOM_MAT2D_NOSITEM = 5753;

	/// <summary>The version of the homogeneous 3D matrix is not supported </summary>
	public const int Jl_ERR_HOM_MAT3D_WRONG_VERSION = 5754;

	/// <summary>Serialized item does not contain a valid homogeneous 3D matrix </summary>
	public const int Jl_ERR_HOM_MAT3D_NOSITEM = 5755;

	/// <summary>The version of the tuple is not supported </summary>
	public const int Jl_ERR_TUPLE_WRONG_VERSION = 5756;

	/// <summary>Serialized item does not contain a valid tuple </summary>
	public const int Jl_ERR_TUPLE_NOSITEM = 5757;

	/// <summary>Number too big for a string to number conversion (overflow) </summary>
	public const int Jl_ERR_TUPLE_DTLFTHV = 5758;

	/// <summary>The version of the camera parameters (pose) is not supported </summary>
	public const int Jl_ERR_POSE_WRONG_VERSION = 5759;

	/// <summary>Serialized item does not contain valid camera parameters (pose) </summary>
	public const int Jl_ERR_POSE_NOSITEM = 5760;

	/// <summary>The version of the internal camera parameters is not supported </summary>
	public const int Jl_ERR_CAM_PAR_WRONG_VERSION = 5761;

	/// <summary>Serialized item does not contain valid internal camera parameters </summary>
	public const int Jl_ERR_CAM_PAR_NOSITEM = 5762;

	/// <summary>The version of the dual quaternion is not supported </summary>
	public const int Jl_ERR_DUAL_QUAT_WRONG_VERSION = 5763;

	/// <summary>Serialized item does not contain a valid dual quaternion</summary>
	public const int Jl_ERR_DUAL_QUAT_NOSITEM = 5764;

	/// <summary>Image source operation failed - unknown reason </summary>
	public const int Jl_ERR_IMGSRC_FAIL = 5800;

	/// <summary>Image source operation failed - wrong internal assumptions </summary>
	public const int Jl_ERR_IMGSRC_LOGIC = 5801;

	/// <summary>Image source functionality is not implemented </summary>
	public const int Jl_ERR_IMGSRC_NOT_IMPLEMENTED = 5802;

	/// <summary>Image source plugin version incompatible </summary>
	public const int Jl_ERR_IMGSRC_INCOMPATIBLE_VERSION = 5803;

	/// <summary>Unhandled exception was triggered by a GenTL producer </summary>
	public const int Jl_ERR_IMGSRC_GENTL_ERROR = 5804;

	/// <summary>Unhandled exception was triggered by the GenICam GenAPI </summary>
	public const int Jl_ERR_IMGSRC_GENAPI_ERROR = 5805;

	/// <summary>Image source resource could not be initialized </summary>
	public const int Jl_ERR_IMGSRC_RES_INIT_FAIL = 5806;

	/// <summary>Image source resource not initialized </summary>
	public const int Jl_ERR_IMGSRC_RES_NOT_INITIALIZED = 5807;

	/// <summary>Image source module request is ambiguous </summary>
	public const int Jl_ERR_IMGSRC_MOD_REQUEST_AMBIGUOUS = 5808;

	/// <summary>Image source module not found </summary>
	public const int Jl_ERR_IMGSRC_MOD_NOT_FOUND = 5809;

	/// <summary>Image source parameter not found </summary>
	public const int Jl_ERR_IMGSRC_PARAM_NOT_FOUND = 5810;

	/// <summary>Image source parameter - wrong value provided </summary>
	public const int Jl_ERR_IMGSRC_PARAM_WRONG_VALUE = 5811;

	/// <summary>Image source parameter - wrong type provided </summary>
	public const int Jl_ERR_IMGSRC_PARAM_WRONG_VALUE_TYPE = 5812;

	/// <summary>Image source parameter - value not readable </summary>
	public const int Jl_ERR_IMGSRC_PARAM_VAL_NOT_READABLE = 5813;

	/// <summary>Image source parameter - value not writable </summary>
	public const int Jl_ERR_IMGSRC_PARAM_VAL_NOT_WRITABLE = 5814;

	/// <summary>Image source parameter - property not available </summary>
	public const int Jl_ERR_IMGSRC_PARAM_PROP_NOT_AVAILABLE = 5815;

	/// <summary>Image source parameter - command timeout </summary>
	public const int Jl_ERR_IMGSRC_COMMAND_TIMEOUT = 5816;

	/// <summary>Image source operation failed - wrong internal state </summary>
	public const int Jl_ERR_IMGSRC_WRONG_STATE = 5817;

	/// <summary>No images received within the configured timeout </summary>
	public const int Jl_ERR_IMGSRC_FETCH_TIMEOUT = 5818;

	/// <summary>Waiting for images aborted </summary>
	public const int Jl_ERR_IMGSRC_FETCH_ABORT = 5819;

	/// <summary>Pixel data conversion failed </summary>
	public const int Jl_ERR_IMGSRC_CONVERSION_FAILED = 5820;

	/// <summary>Access to undefined memory area </summary>
	public const int Jl_ERR_NP = 6000;

	/// <summary>Not enough memory available </summary>
	public const int Jl_ERR_MEM = 6001;

	/// <summary>Memory partition on heap has been overwritten </summary>
	public const int Jl_ERR_ICM = 6002;

	/// <summary>JlAlloc: 0 bytes requested </summary>
	public const int Jl_ERR_WMS = 6003;

	/// <summary>Tmp-memory management: Call freeing memory although nothing had been allocated </summary>
	public const int Jl_ERR_NOTMP = 6004;

	/// <summary>Tmp-memory management: Null pointer while freeing </summary>
	public const int Jl_ERR_TMPNULL = 6005;

	/// <summary>Tmp-memory management: Could not find memory element </summary>
	public const int Jl_ERR_CNFMEM = 6006;

	/// <summary>memory management: wrong memory type </summary>
	public const int Jl_ERR_WMT = 6007;

	/// <summary>Not enough video memory available </summary>
	public const int Jl_ERR_MEM_VID = 6021;

	/// <summary>No memory block allocated at last </summary>
	public const int Jl_ERR_NRA = 6041;

	/// <summary>System parameter for memory-allocation inconsistent </summary>
	public const int Jl_ERR_IAD = 6040;

	/// <summary>Invalid alignment </summary>
	public const int Jl_ERR_INVALID_ALIGN = 6042;

	/// <summary>Function was given a NULL ptr as input </summary>
	public const int Jl_ERR_NULL_PTR = 6043;

	/// <summary>Process creation failed </summary>
	public const int Jl_ERR_CP_FAILED = 6500;

	/// <summary>Wrong index for output control par. </summary>
	public const int Jl_ERR_WOCPI = 7000;

	/// <summary>Wrong number of values: Output control parameter </summary>
	public const int Jl_ERR_WOCPVN = 7001;

	/// <summary>Wrong type: Output control parameter </summary>
	public const int Jl_ERR_WOCPT = 7002;

	/// <summary>Wrong data type for object key (input objects) </summary>
	public const int Jl_ERR_WKT = 7003;

	/// <summary>Range for integer had been passed </summary>
	public const int Jl_ERR_IOOR = 7004;

	/// <summary>Inconsistent Vision version </summary>
	public const int Jl_ERR_IHV = 7005;

	/// <summary>Not enough memory for strings allocated </summary>
	public const int Jl_ERR_NISS = 7006;

	/// <summary>Internal error: Proc is NULL </summary>
	public const int Jl_ERR_PROC_NULL = 7007;

	/// <summary>Unknown symbolic object key (input obj.) </summary>
	public const int Jl_ERR_UNKN = 7105;

	/// <summary>Wrong number of output object parameter </summary>
	public const int Jl_ERR_WOON = 7200;

	/// <summary>Output type &lt;string&gt; expected </summary>
	public const int Jl_ERR_OTSE = 7400;

	/// <summary>Output type &lt;long&gt; expected </summary>
	public const int Jl_ERR_OTLE = 7401;

	/// <summary>Output type &lt;float&gt; expected </summary>
	public const int Jl_ERR_OTFE = 7402;

	/// <summary>Object parameter is a zero pointer </summary>
	public const int Jl_ERR_OPINP = 7403;

	/// <summary>Tuple had been deleted; values are not valid any more </summary>
	public const int Jl_ERR_TWC = 7404;

	/// <summary>CNN: Internal data error </summary>
	public const int Jl_ERR_CNN_DATA = 7701;

	/// <summary>CNN: Invalid memory type </summary>
	public const int Jl_ERR_CNN_MEM = 7702;

	/// <summary>CNN: Invalid data serialization </summary>
	public const int Jl_ERR_CNN_IO_INVALID = 7703;

	/// <summary>CNN: Implementation not available </summary>
	public const int Jl_ERR_CNN_IMPL_NOT_AVAILABLE = 7704;

	/// <summary>CNN: Wrong number of input data </summary>
	public const int Jl_ERR_CNN_NUM_INPUTS_INVALID = 7705;

	/// <summary>CNN: Invalid implementation type </summary>
	public const int Jl_ERR_CNN_IMPL_INVALID = 7706;

	/// <summary>CNN: Training is not supported in the current environment. </summary>
	public const int Jl_ERR_CNN_TRAINING_NOT_SUP = 7707;

	/// <summary>For this operation a GPU with certain minimal requirements is required. See installation guide for details. </summary>
	public const int Jl_ERR_CNN_GPU_REQUIRED = 7708;

	/// <summary>For this operation the CUDA library needs to be available. (See installation guide for details.) </summary>
	public const int Jl_ERR_CNN_CUDA_LIBS_MISSING = 7709;

	/// <summary>OCR File: Error while reading classifier </summary>
	public const int Jl_ERR_OCR_CNN_RE = 7710;

	/// <summary>Wrong generic parameter name </summary>
	public const int Jl_ERR_OCR_CNN_WGPN = 7711;

	/// <summary>One of the parameters returns several values and has to be used exclusively </summary>
	public const int Jl_ERR_OCR_CNN_EXCLUSIV_PARAM = 7712;

	/// <summary>Wrong generic parameter name </summary>
	public const int Jl_ERR_CNN_WGPN = 7713;

	/// <summary>Invalid labels. </summary>
	public const int Jl_ERR_CNN_INVALID_LABELS = 7714;

	/// <summary>OCR File: Wrong file version</summary>
	public const int Jl_ERR_OCR_CNN_FILE_WRONG_VERSION = 7715;

	/// <summary>Invalid classes: At least one class apears twice </summary>
	public const int Jl_ERR_CNN_MULTIPLE_CLASSES = 7716;

	/// <summary>For this operation the cuBLAS library needs to be available. (See installation guide for details.) </summary>
	public const int Jl_ERR_CNN_CUBLAS_LIBS_MISSING = 7717;

	/// <summary>For this operation the CUDNN library needs to be available. (See installation guide for details.) </summary>
	public const int Jl_ERR_CNN_CUDNN_LIBS_MISSING = 7718;

	/// <summary>File 'find_text_support.hotc' not found (please place this file in the ocr subdirectory of the root directory of your Vision installation or in the current working directory) </summary>
	public const int Jl_ERR_OCR_FNF_FIND_TEXT_SUPPORT = 7719;

	/// <summary>Training step failed. This might be caused by unsuitable training parameters </summary>
	public const int Jl_ERR_CNN_TRAINING_FAILED = 7720;

	/// <summary>Weights in Graph have been overwritten previously and are lost. </summary>
	public const int Jl_ERR_CNN_NO_PRETRAINED_WEIGHTS = 7721;

	/// <summary>New input size is too small to produce meaningful features </summary>
	public const int Jl_ERR_CNN_INVALID_INPUT_SIZE = 7722;

	/// <summary>Result is not available. </summary>
	public const int Jl_ERR_CNN_RESULT_NOT_AVAILABLE = 7723;

	/// <summary>New number of channels must be either 1 or 3. </summary>
	public const int Jl_ERR_CNN_INVALID_INPUT_DEPTH = 7724;

	/// <summary>New input number of channels can't be set to 3 if network is specified for number of channels 1 </summary>
	public const int Jl_ERR_CNN_DEPTH_NOT_AVAILABLE = 7725;

	/// <summary>Device batch size larger than batch size. </summary>
	public const int Jl_ERR_CNN_INVALID_BATCH_SIZE = 7726;

	/// <summary>Invalid specification of a parameter. </summary>
	public const int Jl_ERR_CNN_INVALID_PARAM_SPEC = 7727;

	/// <summary>Memory size exceeds maximal allowed value. </summary>
	public const int Jl_ERR_CNN_EXCEEDS_MAX_MEM = 7728;

	/// <summary>New batch size causes integer overflow </summary>
	public const int Jl_ERR_CNN_BATCH_SIZE_OVERFLOW = 7729;

	/// <summary>Invalid input image size for detection model </summary>
	public const int Jl_ERR_CNN_INVALID_IMAGE_SIZE = 7730;

	/// <summary>Invalid parameter value for current layer </summary>
	public const int Jl_ERR_CNN_INVALID_LAYER_PARAM_VALUE = 7731;

	/// <summary>Invalid parameter num for current layer </summary>
	public const int Jl_ERR_CNN_INVALID_LAYER_PARAM_NUM = 7732;

	/// <summary>Invalid parameter type for current layer </summary>
	public const int Jl_ERR_CNN_INVALID_LAYER_PARAM_TYPE = 7733;

	/// <summary>CNN: Wrong number of output data </summary>
	public const int Jl_ERR_CNN_NUM_OUTPUTS_INVALID = 7734;

	/// <summary>CNN: Invalid input shape </summary>
	public const int Jl_ERR_CNN_INVALID_SHAPE = 7735;

	/// <summary>CNN: Invalid input data </summary>
	public const int Jl_ERR_CNN_INVALID_INPUT_DATA = 7736;

	/// <summary>CNN: For variable input lengths the ctc loss layer only computes correct gradients if the used cuDNN version is &gt;= 7.6.3. Please upgrade cuDNN or do not use variable input lengths. </summary>
	public const int Jl_ERR_CNN_CUDNN_CTC_LOSS_BUGGY = 7737;

	/// <summary>CNN: Invalid padding </summary>
	public const int Jl_ERR_CNN_INVALID_PADDING = 7738;

	/// <summary>CNN: Invalid layer type serialization </summary>
	public const int Jl_ERR_CNN_IO_INVALID_LAYER_TYPE = 7740;

	/// <summary>CNN: Inference failed </summary>
	public const int Jl_ERR_CNN_INFERENCE_FAILED = 7741;

	/// <summary>CNN: Runtime not supported on this machine </summary>
	public const int Jl_ERR_CNN_RUNTIME_FAILED = 7742;

	/// <summary>Graph: Internal error </summary>
	public const int Jl_ERR_GRAPH_INTERNAL = 7751;

	/// <summary>Graph: Invalid data serialization </summary>
	public const int Jl_ERR_GRAPH_IO_INVALID = 7752;

	/// <summary>Graph: Invalid index </summary>
	public const int Jl_ERR_GRAPH_INVALID_INDEX = 7753;

	/// <summary>JlCNNGraph: Internal error </summary>
	public const int Jl_ERR_CNNGRAPH_INTERNAL = 7760;

	/// <summary>JlCNNGraph: Invalid data serialization </summary>
	public const int Jl_ERR_CNNGRAPH_IO_INVALID = 7761;

	/// <summary>JlCNNGraph: Invalid layer specification </summary>
	public const int Jl_ERR_CNNGRAPH_LAYER_INVALID = 7762;

	/// <summary>JlCNNGraph: Graph not properly initialized </summary>
	public const int Jl_ERR_CNNGRAPH_NOINIT = 7763;

	/// <summary>CNN-Graph: Invalid memory type </summary>
	public const int Jl_ERR_CNNGRAPH_INVALID_MEM = 7764;

	/// <summary>CNN-Graph: Invalid number of layers </summary>
	public const int Jl_ERR_CNNGRAPH_INVALID_NUML = 7765;

	/// <summary>CNN-Graph: Invalid index </summary>
	public const int Jl_ERR_CNNGRAPH_INVALID_IDX = 7766;

	/// <summary>CNN-Graph: Invalid specification status </summary>
	public const int Jl_ERR_CNNGRAPH_SPEC_STATUS = 7767;

	/// <summary>CNN-Graph: Graph is not allowed to be changed after initialization </summary>
	public const int Jl_ERR_CNNGRAPH_NOCHANGE = 7768;

	/// <summary>CNN-Graph: Missing preprocessing </summary>
	public const int Jl_ERR_CNNGRAPH_PREPROC = 7769;

	/// <summary>CNN-Graph: Invalid vertex degree </summary>
	public const int Jl_ERR_CNNGRAPH_DEGREE = 7770;

	/// <summary>CNN-Graph: Invalid output shape </summary>
	public const int Jl_ERR_CNNGRAPH_OUTSHAPE = 7771;

	/// <summary>CNN-Graph: Invalid specification </summary>
	public const int Jl_ERR_CNNGRAPH_SPEC = 7772;

	/// <summary>CNN-Graph: Invalid graph definition </summary>
	public const int Jl_ERR_CNNGRAPH_DEF = 7773;

	/// <summary>CNN-Graph: Architecture not suitable for the adaption of the number of output classes </summary>
	public const int Jl_ERR_CNNGRAPH_NO_CLASS_CHANGE = 7774;

	/// <summary>CNN-Graph: Architecture not suitable for the adaption of the image size" </summary>
	public const int Jl_ERR_CNNGRAPH_NO_IMAGE_RESIZE = 7775;

	/// <summary>CNN-Graph: Aux index out of bounds. </summary>
	public const int Jl_ERR_CNNGRAPH_AUX_INDEX_OOB = 7776;

	/// <summary>CNN-Graph: Invalid graph definition. Probably the auxiliary outputs of a layer have not been connected with corresponding aux selection layers (SelectAux) or at least one aux output has not been specified during model creation (create_dl_model call). </summary>
	public const int Jl_ERR_CNNGRAPH_AUX_SPEC = 7777;

	/// <summary>CNN-Graph: Layer not supported for selected runtime </summary>
	public const int Jl_ERR_CNNGRAPH_LAYER_UNSUPPORTED = 7778;

	/// <summary>DL: Internal error </summary>
	public const int Jl_ERR_DL_INTERNAL = 7779;

	/// <summary>DL: Error reading file </summary>
	public const int Jl_ERR_DL_FILE_READ = 7780;

	/// <summary>DL: Error writing file </summary>
	public const int Jl_ERR_DL_FILE_WRITE = 7781;

	/// <summary>DL: Wrong file version </summary>
	public const int Jl_ERR_DL_FILE_WRONG_VERSION = 7782;

	/// <summary>DL: Inputs missing in input dict </summary>
	public const int Jl_ERR_DL_INPUTS_MISSING = 7783;

	/// <summary>DL: Inputs have incorrect batch size </summary>
	public const int Jl_ERR_DL_INPUT_WRONG_BS = 7784;

	/// <summary>DL: Invalid layer name </summary>
	public const int Jl_ERR_DL_INVALID_NAME = 7785;

	/// <summary>DL: Duplicate layer name </summary>
	public const int Jl_ERR_DL_DUPLICATE_NAME = 7786;

	/// <summary>DL: Invalid output layer </summary>
	public const int Jl_ERR_DL_INVALID_OUTPUT = 7787;

	/// <summary>DL: Parameter is not available </summary>
	public const int Jl_ERR_DL_PARAM_NOT_AVAILABLE = 7788;

	/// <summary>DL: Tuple inputs have incorrect length </summary>
	public const int Jl_ERR_DL_INPUT_WRONG_LENGTH = 7789;

	/// <summary>DL: Tuple inputs have incorrect type </summary>
	public const int Jl_ERR_DL_INPUT_WRONG_TYPE = 7790;

	/// <summary>DL: Some inputs have incorrect values </summary>
	public const int Jl_ERR_DL_INPUT_WRONG_VALUES = 7791;

	/// <summary>DL: Some class ids are not unique </summary>
	public const int Jl_ERR_DL_CLASS_IDS_NOT_UNIQUE = 7792;

	/// <summary>DL: Some class ids are invalid </summary>
	public const int Jl_ERR_DL_CLASS_IDS_INVALID = 7793;

	/// <summary>DL: Input data of class id conversion is invalid. </summary>
	public const int Jl_ERR_DL_CLASS_IDS_INVALID_CONV = 7794;

	/// <summary>DL: Type already defined </summary>
	public const int Jl_ERR_DL_TYPE_ALREADY_DEFINED = 7795;

	/// <summary>DL: Cannot identify inference inputs. </summary>
	public const int Jl_ERR_DL_NO_INFERENCE_INPUTS = 7796;

	/// <summary>DL: Some class ids overlap with ignore class ids. </summary>
	public const int Jl_ERR_DL_CLASS_IDS_INVALID_OVERLAP = 7797;

	/// <summary>DL: Wrong number of output layer </summary>
	public const int Jl_ERR_DL_WRONG_OUTPUT_LAYER_NUM = 7798;

	/// <summary>DL: Batch size multiplier needs to be greater than 0 </summary>
	public const int Jl_ERR_DL_WRONG_BS_MULTIPLIER = 7799;

	/// <summary>DL: Inputs have incorrect batch size. The number of needed inputs is defined by batch_size * batch_size_multiplier </summary>
	public const int Jl_ERR_DL_INPUT_WRONG_BS_WITH_MULTIPLIER = 7800;

	/// <summary>Error occurred while reading an ONNX model </summary>
	public const int Jl_ERR_DL_READ_ONNX = 7801;

	/// <summary>DL: Model does not have class ids </summary>
	public const int Jl_ERR_DL_CLASS_IDS_MISSING = 7802;

	/// <summary>Error occurred while writing an ONNX model </summary>
	public const int Jl_ERR_DL_WRITE_ONNX = 7803;

	/// <summary>DL: Libprotobuf for ONNX could not be loaded </summary>
	public const int Jl_ERR_DL_ONNX_LOADER = 7804;

	/// <summary>DL: Wrong scales during FPN creation </summary>
	public const int Jl_ERR_DL_FPN_SCALES = 7810;

	/// <summary>DL: Backbone unusable for FPN creation </summary>
	public const int Jl_ERR_DL_FPN_INVALID_BACKBONE = 7811;

	/// <summary>DL: Backbone feature maps not divisible by 2 </summary>
	public const int Jl_ERR_DL_FPN_INVALID_FEATURE_MAP_SIZE = 7812;

	/// <summary>Invalid FPN levels given </summary>
	public const int Jl_ERR_DL_FPN_INVALID_LEVELS = 7813;

	/// <summary>DL: Internal error using anchors </summary>
	public const int Jl_ERR_DL_ANCHOR = 7820;

	/// <summary>DL: Invalid detector parameter </summary>
	public const int Jl_ERR_DL_DETECTOR_INVALID_PARAM = 7821;

	/// <summary>DL: Invalid detector parameter value </summary>
	public const int Jl_ERR_DL_DETECTOR_INVALID_PARAM_VALUE = 7822;

	/// <summary>DL: Invalid docking layer </summary>
	public const int Jl_ERR_DL_DETECTOR_INVALID_DOCKING_LAYER = 7823;

	/// <summary>DL: Invalid instance type </summary>
	public const int Jl_ERR_DL_DETECTOR_INVALID_INSTANCE_TYPE = 7824;

	/// <summary>DL-Node: Missing generic parameter 'name'. Please specify a layer name. </summary>
	public const int Jl_ERR_DL_NODE_MISSING_PARAM_NAME = 7830;

	/// <summary>DL-Node: No generic parameter 'name' allowed for this node. </summary>
	public const int Jl_ERR_DL_NODE_GENPARAM_NAME_NOT_ALLOWED = 7831;

	/// <summary>DL-Node: Invalid layer specification. </summary>
	public const int Jl_ERR_DL_NODE_INVALID_SPEC = 7832;

	/// <summary>DL-Node: There can only be one direct connection between two layers.</summary>
	public const int Jl_ERR_DL_NODE_DUPLICATE_EDGE = 7833;

	/// <summary>DL-Solver: Invalid type. </summary>
	public const int Jl_ERR_DL_SOLVER_INVALID_TYPE = 7840;

	/// <summary>DL-Solver: Invalid update formula. </summary>
	public const int Jl_ERR_DL_SOLVER_INVALID_UPDATE_FORMULA = 7841;

	/// <summary>DL: Heatmap is unsupported with the selected runtime. </summary>
	public const int Jl_ERR_DL_HEATMAP_UNSUPPORTED_RUNTIME = 7850;

	/// <summary>DL: Unsupported heatmap model type. The heatmap is only applicable for model type 'classification'. </summary>
	public const int Jl_ERR_DL_HEATMAP_UNSUPPORTED_MODEL_TYPE = 7851;

	/// <summary>DL: Unsupported heatmap method </summary>
	public const int Jl_ERR_DL_HEATMAP_UNSUPPORTED_METHOD = 7852;

	/// <summary>DL: Wrong target class id for heatmap </summary>
	public const int Jl_ERR_DL_HEATMAP_WRONG_TARGET_CLASS_ID = 7853;

	/// <summary>DL: GC Anomaly Detection network not available </summary>
	public const int Jl_ERR_DL_GCAD_NETWORK_NOT_AVAILABLE = 7870;

	/// <summary>DL: Internal error occurred in anomaly model </summary>
	public const int Jl_ERR_DL_ANOMALY_MODEL_INTERNAL = 7880;

	/// <summary>DL: Untrained anomaly model </summary>
	public const int Jl_ERR_DL_ANOMALY_MODEL_UNTRAINED = 7881;

	/// <summary>DL: Anomaly model training failed </summary>
	public const int Jl_ERR_DL_ANOMALY_MODEL_TRAINING_FAILED = 7882;

	/// <summary>DL: Unable to set parameter on a trained anomaly detection model </summary>
	public const int Jl_ERR_DL_ANOMALY_MODEL_PARAM_TRAINED = 7883;

	/// <summary>DL: Input image size cannot be changed </summary>
	public const int Jl_ERR_DL_ANOMALY_MODEL_RESIZE = 7884;

	/// <summary>DL: Input depth is not supported </summary>
	public const int Jl_ERR_DL_ANOMALY_MODEL_DEPTH = 7885;

	/// <summary>DL: Input domain must not be empty </summary>
	public const int Jl_ERR_DL_ANOMALY_MODEL_INPUT_DOMAIN = 7886;

	/// <summary>Deep OCR internal error </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_INTERNAL = 7890;

	/// <summary>Each entry of the alphabet can only contain a string of length one. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET = 7891;

	/// <summary>Out of bounds index into alphabet. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET_IDX = 7892;

	/// <summary>The type of the given DL model is not allowed. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_MODEL_TYPE = 7893;

	/// <summary>The model is not available. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_NOT_AVAILABLE = 7894;

	/// <summary>It is not possible to specify a mapping because there is no internal alphabet specified. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET_MAPPING_NO_ALPHABET = 7895;

	/// <summary>Out of bounds index into alphabet given as mapping. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET_MAPPING_IDX = 7896;

	/// <summary>The length of the mapping and the length of the internal alphabet need to be the same. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET_MAPPING_LEN = 7897;

	/// <summary>The model file cannot be found. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_FILE_NOT_FOUND = 7898;

	/// <summary>Some character is not part of the internal alphabet. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_UNKNOWN_CHAR = 7899;

	/// <summary>The given word length is invalid. </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_WORD_LENGTH = 7900;

	/// <summary>The given alphabet is not a unique list of characters </summary>
	public const int Jl_ERR_DEEP_OCR_MODEL_ALPHABET_NOT_UNIQUE = 7901;

	/// <summary>apply_dl_model: no default outputs allowed </summary>
	public const int Jl_ERR_DL_MODEL_APPLY_NO_DEF_OUTPUTS = 7910;

	/// <summary>DL: Unsupported generic parameter </summary>
	public const int Jl_ERR_DL_MODEL_UNSUPPORTED_GENPARAM = 7911;

	/// <summary>DL: Operator does not support model </summary>
	public const int Jl_ERR_DL_MODEL_OPERATOR_UNSUPPORTED = 7912;

	/// <summary>DL: Requested runtime cannot be set </summary>
	public const int Jl_ERR_DL_MODEL_RUNTIME = 7913;

	/// <summary>DL: Unsupported generic value(s) </summary>
	public const int Jl_ERR_DL_MODEL_UNSUPPORTED_GENVALUE = 7914;

	/// <summary>DL: Invalid number of samples </summary>
	public const int Jl_ERR_DL_MODEL_INVALID_NUM_SAMPLES = 7915;

	/// <summary>DL: Parameter unsupported for converted model </summary>
	public const int Jl_ERR_DL_MODEL_CONVERTED_PARAM = 7916;

	/// <summary>DL: Unsupported operation on converted model </summary>
	public const int Jl_ERR_DL_MODEL_CONVERTED_UNSUPPORTED = 7917;

	/// <summary>DL: The given dataset is incorrect </summary>
	public const int Jl_ERR_DL_INVALID_DATASET = 7925;

	/// <summary>DL: Invalid sample index </summary>
	public const int Jl_ERR_DL_INVALID_SAMPLE_INDEX = 7926;

	/// <summary>DL: Transform name is invalid </summary>
	public const int Jl_ERR_DL_TRANSFORM_INVALID_NAME = 7931;

	/// <summary>DL: Transform parameter is not available </summary>
	public const int Jl_ERR_DL_TRANSFORM_PARAM_NOT_AVAILABLE = 7932;

	/// <summary>DL: Transform pipeline parameter is not available </summary>
	public const int Jl_ERR_DL_TRANSFORM_PIPELINE_PARAM_NOT_AVAILABLE = 7933;

	/// <summary>DL: Invalid type of transform during read </summary>
	public const int Jl_ERR_CNN_IO_INVALID_TRANSFORM_TYPE = 7934;

	/// <summary>Deep Counting model is not prepared </summary>
	public const int Jl_ERR_DEEP_COUNTING_NOT_PREPARED = 7940;

	/// <summary>The chosen backbone is not settable </summary>
	public const int Jl_ERR_DEEP_COUNTING_UNSUPPORTED_BACKBONE = 7941;

	/// <summary>Usage of prepare for a Deep Counting model is unsupported </summary>
	public const int Jl_ERR_DEEP_COUNTING_PREPARE_UNSUPPORTED = 7942;

	/// <summary>Deep Counting model does not contain a backbone </summary>
	public const int Jl_ERR_DEEP_COUNTING_NO_BACKBONE = 7943;

	/// <summary>DL: Unsupported device precision </summary>
	public const int Jl_ERR_DL_DEVICE_UNSUPPORTED_PRECISION = 7960;

	/// <summary>DL: Invalid model for continual learning </summary>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_UNSUPPORTED_MODEL = 7970;

	/// <summary>DL: Model has not been initialized for continual learning </summary>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_MODEL_NOT_INITIALIZED = 7971;

	/// <summary>DL: Model has already been initialized for continual learning </summary>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_MODEL_ALREADY_INITIALIZED = 7972;

	/// <summary>DL: Continual Learning inference failed </summary>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_INFERENCE_FAILED = 7973;

	/// <summary>DL: Operation invalidates continual learning. </summary>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_INVALID = 7974;

	/// <summary>DL: Insufficient diverse samples for continual learning either init or continual operators </summary>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_INSUFFICIENT_SAMPLE_DIVERSITY = 7975;

	/// <summary>DL: Pruning data does not fit the given model </summary>
	public const int Jl_ERR_DL_PRUNING_WRONG_DATA = 7980;

	/// <summary>DL: Model architecture does not support pruning </summary>
	public const int Jl_ERR_DL_PRUNING_UNSUPPORTED_BY_CNN = 7981;

	/// <summary>DL: Invalid model type for out-of-distribution detection </summary>
	public const int Jl_ERR_DL_OOD_UNSUPPORTED_MODEL_TYPE = 7985;

	/// <summary>DL: Insufficient diverse samples for fitting out-of-distribution detection </summary>
	public const int Jl_ERR_DL_OOD_INSUFFICIENT_SAMPLE_DIVERSITY = 7986;

	/// <summary>DL: Internal error in the calculation of out-of-distribution detection. </summary>
	public const int Jl_ERR_DL_OOD_INTERNAL_ERROR = 7987;

	/// <summary>DL: Operation invalidates out-of-distribution detection. </summary>
	public const int Jl_ERR_DL_OOD_INVALID = 7988;

	/// <summary>DLModule is not loaded </summary>
	public const int Jl_ERR_DL_MODULE_NOT_LOADED = 7990;

	/// <summary>Unknown operator name </summary>
	public const int Jl_ERR_WPRN = 8000;

	/// <summary>register_comp_used is not activated </summary>
	public const int Jl_ERR_RCNA = 8001;

	/// <summary>Unknown operator class </summary>
	public const int Jl_ERR_WPC = 8002;

	/// <summary>convol/mask: Error while opening file </summary>
	public const int Jl_ERR_ORMF = 8101;

	/// <summary>convol/mask: Premature end of file </summary>
	public const int Jl_ERR_EOFRMF = 8102;

	/// <summary>convol/mask: Conversion error </summary>
	public const int Jl_ERR_CVTRMF = 8103;

	/// <summary>convol/mask: Wrong row-/column number </summary>
	public const int Jl_ERR_LCNRMF = 8104;

	/// <summary>convol/mask: Mask size overflow </summary>
	public const int Jl_ERR_WCOVRMF = 8105;

	/// <summary>convol/mask: Too many elements entered </summary>
	public const int Jl_ERR_NEOFRMF = 8106;

	/// <summary>convol: Wrong margin type </summary>
	public const int Jl_ERR_WRRA = 8107;

	/// <summary>convol: No mask object has got empty region </summary>
	public const int Jl_ERR_MCN0 = 8108;

	/// <summary>convol: Weight factor is 0 </summary>
	public const int Jl_ERR_WF0 = 8110;

	/// <summary>convol: Inconsistent number of weights </summary>
	public const int Jl_ERR_NWC = 8111;

	/// <summary>rank: Wrong rank value </summary>
	public const int Jl_ERR_WRRV = 8112;

	/// <summary>convol/rank: Error while handling margin </summary>
	public const int Jl_ERR_ROVFL = 8113;

	/// <summary>Error while parsing filter mask file </summary>
	public const int Jl_ERR_EWPMF = 8114;

	/// <summary>Wrong number of coefficients for convolution (sigma too big?) </summary>
	public const int Jl_ERR_WNUMM = 8120;

	/// <summary>No valid ID for data set </summary>
	public const int Jl_ERR_WBEDN = 8200;

	/// <summary>No data set active (set_bg_esti) </summary>
	public const int Jl_ERR_NBEDA = 8201;

	/// <summary>ID already used for data set </summary>
	public const int Jl_ERR_BEDNAU = 8202;

	/// <summary>No data set created (create_bg_esti) </summary>
	public const int Jl_ERR_NBEDC = 8204;

	/// <summary>Not possible to pass an object list </summary>
	public const int Jl_ERR_NTM = 8205;

	/// <summary>Image has other size than the background image in data set </summary>
	public const int Jl_ERR_WISBE = 8206;

	/// <summary>Up-date-region is bigger than background image </summary>
	public const int Jl_ERR_UDNSSBE = 8207;

	/// <summary>Number of statistic data sets is too small </summary>
	public const int Jl_ERR_SNBETS = 8208;

	/// <summary>Wrong value for adapt mode </summary>
	public const int Jl_ERR_WAMBE = 8209;

	/// <summary>Wrong value for frame mode </summary>
	public const int Jl_ERR_WFMBE = 8210;

	/// <summary>Number of point corresponcences too small </summary>
	public const int Jl_ERR_PE_NPCTS = 8250;

	/// <summary>Invalid method </summary>
	public const int Jl_ERR_PE_INVMET = 8251;

	/// <summary>Maximum number of fonts exceeded </summary>
	public const int Jl_ERR_OCR_MEM1 = 8300;

	/// <summary>Wrong ID (Number) for font </summary>
	public const int Jl_ERR_OCR_WID = 8301;

	/// <summary>OCR internal error: wrong ID </summary>
	public const int Jl_ERR_OCR1 = 8302;

	/// <summary>OCR not initialised: no font was read in </summary>
	public const int Jl_ERR_OCR_NNI = 8303;

	/// <summary>No font activated </summary>
	public const int Jl_ERR_OCR_NAI = 8304;

	/// <summary>OCR internal error: Wrong threshold in angle determination </summary>
	public const int Jl_ERR_OCR_WTP = 8305;

	/// <summary>OCR internal error: Wrong attribute </summary>
	public const int Jl_ERR_OCR_WF = 8306;

	/// <summary>The version of the OCR classifier is not supported </summary>
	public const int Jl_ERR_OCR_READ = 8307;

	/// <summary>OCR File: Inconsistent number of nodes </summary>
	public const int Jl_ERR_OCR_NODES = 8308;

	/// <summary>OCR File: File too short </summary>
	public const int Jl_ERR_OCR_EOF = 8309;

	/// <summary>OCR: Internal error 1 </summary>
	public const int Jl_ERR_OCR_INC1 = 8310;

	/// <summary>OCR: Internal error 2 </summary>
	public const int Jl_ERR_OCR_INC2 = 8311;

	/// <summary>Wrong type of OCR tool (no 'box' or 'net') </summary>
	public const int Jl_ERR_WOCRTYPE = 8312;

	/// <summary>The version of the OCR training characters is not supported </summary>
	public const int Jl_ERR_OCR_TRF = 8313;

	/// <summary>Image too large for training file </summary>
	public const int Jl_ERR_TRF_ITL = 8314;

	/// <summary>Region too large for training file </summary>
	public const int Jl_ERR_TRF_RTL = 8315;

	/// <summary>Protected OCR training file </summary>
	public const int Jl_ERR_TRF_PT = 8316;

	/// <summary>Protected OCR training file: wrong passw. </summary>
	public const int Jl_ERR_TRF_WPW = 8317;

	/// <summary>Serialized item does not contain a valid OCR classifier </summary>
	public const int Jl_ERR_OCR_NOSITEM = 8318;

	/// <summary>OCR training file concatenation failed: identical input and output files </summary>
	public const int Jl_ERR_TRF_CON_EIO = 8319;

	/// <summary>Invalid file format for MLP classifier </summary>
	public const int Jl_ERR_OCR_MLP_NOCLASSFILE = 8320;

	/// <summary>The version of the MLP classifier is not supported </summary>
	public const int Jl_ERR_OCR_MLP_WRCLASSVERS = 8321;

	/// <summary>Serialized item does not contain a valid MLP classifier </summary>
	public const int Jl_ERR_OCR_MLP_NOSITEM = 8322;

	/// <summary>Invalid file format for SVM classifier </summary>
	public const int Jl_ERR_OCR_SVM_NOCLASSFILE = 8330;

	/// <summary>The version of the SVM classifier is not supported</summary>
	public const int Jl_ERR_OCR_SVM_WRCLASSVERS = 8331;

	/// <summary>Serialized item does not contain a valid SVM classifier </summary>
	public const int Jl_ERR_OCR_SVM_NOSITEM = 8332;

	/// <summary>Invalid file format for k-NN classifier </summary>
	public const int Jl_ERR_OCR_KNN_NOCLASSFILE = 8333;

	/// <summary>Serialized item does not contain a valid k-NN classifier </summary>
	public const int Jl_ERR_OCR_KNN_NOSITEM = 8334;

	/// <summary>Invalid file format for CNN classifier </summary>
	public const int Jl_ERR_OCR_CNN_NOCLASSFILE = 8335;

	/// <summary>The version of the CNN classifier is not supported </summary>
	public const int Jl_ERR_OCR_CNN_WRCLASSVERS = 8336;

	/// <summary>Serialized item does not contain a valid CNN classifier </summary>
	public const int Jl_ERR_OCR_CNN_NOSITEM = 8337;

	/// <summary>Result name is not available for this mode </summary>
	public const int Jl_ERR_OCR_RESULT_NOT_AVAILABLE = 8338;

	/// <summary>OCV system not initialized </summary>
	public const int Jl_ERR_OCV_NI = 8350;

	/// <summary>The version of the OCV tool is not supported </summary>
	public const int Jl_ERR_WOCVTYPE = 8351;

	/// <summary>Wrong name for an OCV object </summary>
	public const int Jl_ERR_OCV_WNAME = 8353;

	/// <summary>Training has already been applied </summary>
	public const int Jl_ERR_OCV_II = 8354;

	/// <summary>No training has been applied </summary>
	public const int Jl_ERR_OCV_NOTTR = 8355;

	/// <summary>Serialized item does not contain a valid OCV tool </summary>
	public const int Jl_ERR_OCV_NOSITEM = 8356;

	/// <summary>Wrong number of function points </summary>
	public const int Jl_ERR_WLENGTH = 8370;

	/// <summary>List of values is not a function </summary>
	public const int Jl_ERR_NO_FUNCTION = 8371;

	/// <summary>Wrong ordering of values (not ascending)</summary>
	public const int Jl_ERR_NOT_ASCENDING = 8372;

	/// <summary>Illegal distance of function points </summary>
	public const int Jl_ERR_ILLEGAL_DIST = 8373;

	/// <summary>Function is not monotonic. </summary>
	public const int Jl_ERR_NOT_MONOTONIC = 8374;

	/// <summary>Wrong function type. </summary>
	public const int Jl_ERR_WFUNCTION = 8375;

	/// <summary>Same x-value due to double to float conversion. </summary>
	public const int Jl_ERR_SAME_XVAL_CONV = 8376;

	/// <summary>The input points could not be arranged in a regular grid </summary>
	public const int Jl_ERR_GRID_CONNECT_POINTS = 8390;

	/// <summary>Error while creating the output map </summary>
	public const int Jl_ERR_GRID_GEN_MAP = 8391;

	/// <summary>Auto rotation failed </summary>
	public const int Jl_ERR_GRID_AUTO_ROT = 8392;

	/// <summary>No common camera parameters </summary>
	public const int Jl_ERR_CAL_NO_COMM_PAR = 8393;

	/// <summary>Vy must be &gt; 0 </summary>
	public const int Jl_ERR_CAL_NEGVY = 8394;

	/// <summary>Same finder pattern found multiple times </summary>
	public const int Jl_ERR_CAL_IDENTICAL_FP = 8395;

	/// <summary>Function not available for line scan cameras with perspective lenses </summary>
	public const int Jl_ERR_CAL_LSCPNA = 8396;

	/// <summary>Mark segmentation failed </summary>
	public const int Jl_ERR_CAL_MARK_SEGM = 8397;

	/// <summary>Contour extraction failed </summary>
	public const int Jl_ERR_CAL_CONT_EXT = 8398;

	/// <summary>No finder pattern found </summary>
	public const int Jl_ERR_CAL_NO_FP = 8399;

	/// <summary>At least 3 calibration points have to be indicated </summary>
	public const int Jl_ERR_CAL_LCALP = 8400;

	/// <summary>Inconsistent finder pattern positions </summary>
	public const int Jl_ERR_CAL_INCONSISTENT_FP = 8401;

	/// <summary>No calibration table found </summary>
	public const int Jl_ERR_CAL_NCPF = 8402;

	/// <summary>Error while reading calibration table description file </summary>
	public const int Jl_ERR_CAL_RECPF = 8403;

	/// <summary>Minimum threshold while searching for ellipses </summary>
	public const int Jl_ERR_CAL_LTMTH = 8404;

	/// <summary>Read error / format error in calibration table description file </summary>
	public const int Jl_ERR_CAL_FRCP = 8405;

	/// <summary>Error in projection: s_x = 0 or s_y = 0 or z = 0 </summary>
	public const int Jl_ERR_CAL_PROJ = 8406;

	/// <summary>Error in inverse projection </summary>
	public const int Jl_ERR_CAL_UNPRO = 8407;

	/// <summary>Not possible to open camera parameter file </summary>
	public const int Jl_ERR_CAL_RICPF = 8408;

	/// <summary>Format error in file: No colon </summary>
	public const int Jl_ERR_CAL_FICP1 = 8409;

	/// <summary>Format error in file: 2. colon is missing </summary>
	public const int Jl_ERR_CAL_FICP2 = 8410;

	/// <summary>Format error in file: Semicolon is missing </summary>
	public const int Jl_ERR_CAL_FICP3 = 8411;

	/// <summary>Not possible to open camera parameter (pose) file </summary>
	public const int Jl_ERR_CAL_REPOS = 8412;

	/// <summary>Format error in camera parameter (pose) file </summary>
	public const int Jl_ERR_CAL_FOPOS = 8413;

	/// <summary>Not possible to open calibration target description file </summary>
	public const int Jl_ERR_CAL_OCPDF = 8414;

	/// <summary>Not possible to open postscript file of calibration target </summary>
	public const int Jl_ERR_CAL_OCPPS = 8415;

	/// <summary>Error while norming the vector </summary>
	public const int Jl_ERR_CAL_EVECN = 8416;

	/// <summary>Fitting of calibration target failed </summary>
	public const int Jl_ERR_CAL_NPLAN = 8417;

	/// <summary>No next mark found </summary>
	public const int Jl_ERR_CAL_NNMAR = 8418;

	/// <summary>Normal equation system is not solvable </summary>
	public const int Jl_ERR_CAL_NNEQU = 8419;

	/// <summary>Average quadratic error is too big for 3D position of mark </summary>
	public const int Jl_ERR_CAL_QETHM = 8420;

	/// <summary>Non elliptic contour </summary>
	public const int Jl_ERR_CAL_NOELL = 8421;

	/// <summary>Wrong parameter value slvand() </summary>
	public const int Jl_ERR_CAL_WPARV = 8422;

	/// <summary>Wrong function results slvand() </summary>
	public const int Jl_ERR_CAL_WFRES = 8423;

	/// <summary>Distance of marks in calibration target description file is not possible </summary>
	public const int Jl_ERR_CAL_ECPDI = 8424;

	/// <summary>Specified flag for degree of freedom not valid </summary>
	public const int Jl_ERR_CAL_WEFLA = 8425;

	/// <summary>Minimum error did not fall below </summary>
	public const int Jl_ERR_CAL_NOMER = 8426;

	/// <summary>Wrong type in Pose (rotation / translation) </summary>
	public const int Jl_ERR_CAL_WPTYP = 8427;

	/// <summary>Image size does not match the measurement in camera parameters </summary>
	public const int Jl_ERR_CAL_WIMSZ = 8428;

	/// <summary>Point could not be projected into linescan image </summary>
	public const int Jl_ERR_CAL_NPILS = 8429;

	/// <summary>Diameter of calibration marks could not be determined </summary>
	public const int Jl_ERR_CAL_DIACM = 8430;

	/// <summary>Orientation of calibration plate could not be determined </summary>
	public const int Jl_ERR_CAL_ORICP = 8431;

	/// <summary>Calibration plate does not lie completely inside the image </summary>
	public const int Jl_ERR_CAL_CPNII = 8432;

	/// <summary>Wrong number of calibration marks extracted </summary>
	public const int Jl_ERR_CAL_WNCME = 8433;

	/// <summary>Unknown name of parameter group </summary>
	public const int Jl_ERR_CAL_UNKPG = 8434;

	/// <summary>Focal length must be non-negative </summary>
	public const int Jl_ERR_CAL_NEGFL = 8435;

	/// <summary>Function not available for cameras with telecentric lenses </summary>
	public const int Jl_ERR_CAL_TELNA = 8436;

	/// <summary>Function not available for line scan cameras </summary>
	public const int Jl_ERR_CAL_LSCNA = 8437;

	/// <summary>Ellipse is degenerated to a point </summary>
	public const int Jl_ERR_CAL_ELLDP = 8438;

	/// <summary>No orientation mark found </summary>
	public const int Jl_ERR_CAL_NOMF = 8439;

	/// <summary>Camera calibration did not converge </summary>
	public const int Jl_ERR_CAL_NCONV = 8440;

	/// <summary>Function not available for cameras with hypercentric lenses </summary>
	public const int Jl_ERR_CAL_HYPNA = 8441;

	/// <summary>Point cannot be distorted. </summary>
	public const int Jl_ERR_CAL_DISTORT = 8442;

	/// <summary>Wrong edge filter. </summary>
	public const int Jl_ERR_CAL_WREDGFILT = 8443;

	/// <summary>Pixel size must be non-negative or zero </summary>
	public const int Jl_ERR_CAL_NEGPS = 8444;

	/// <summary>Tilt is in the wrong range </summary>
	public const int Jl_ERR_CAL_NEGTS = 8445;

	/// <summary>Rot is in the wrong range </summary>
	public const int Jl_ERR_CAL_NEGRS = 8446;

	/// <summary>Camera parameters are invalid </summary>
	public const int Jl_ERR_CAL_INVCAMPAR = 8447;

	/// <summary>Focal length must be positive </summary>
	public const int Jl_ERR_CAL_ILLFL = 8448;

	/// <summary>Magnification must be positive </summary>
	public const int Jl_ERR_CAL_ILLMAG = 8449;

	/// <summary>Illegal image plane distance </summary>
	public const int Jl_ERR_CAL_ILLIPD = 8450;

	/// <summary>model not optimized yet - no res's</summary>
	public const int Jl_ERR_CM_NOT_OPTIMIZED = 8451;

	/// <summary>auxiliary model results not available </summary>
	public const int Jl_ERR_CM_NOT_POSTPROCC = 8452;

	/// <summary>setup not 'visibly' interconnected </summary>
	public const int Jl_ERR_CM_NOT_INTERCONN = 8453;

	/// <summary>camera parameter mismatch </summary>
	public const int Jl_ERR_CM_CAMPAR_MISMCH = 8454;

	/// <summary>camera type mismatch </summary>
	public const int Jl_ERR_CM_CAMTYP_MISMCH = 8455;

	/// <summary>camera type not supported </summary>
	public const int Jl_ERR_CM_CAMTYP_UNSUPD = 8456;

	/// <summary>invalid camera ID </summary>
	public const int Jl_ERR_CM_INVALD_CAMIDX = 8457;

	/// <summary>invalid cal.obj. ID </summary>
	public const int Jl_ERR_CM_INVALD_DESCID = 8458;

	/// <summary>invalid cal.obj. instance ID </summary>
	public const int Jl_ERR_CM_INVALD_COBJID = 8459;

	/// <summary>undefined camera </summary>
	public const int Jl_ERR_CM_UNDEFINED_CAM = 8460;

	/// <summary>repeated observ. index </summary>
	public const int Jl_ERR_CM_REPEATD_INDEX = 8461;

	/// <summary>undefined calib. object description </summary>
	public const int Jl_ERR_CM_UNDEFI_CADESC = 8462;

	/// <summary>Invalid file format for calibration data model </summary>
	public const int Jl_ERR_CM_NO_DESCR_FILE = 8463;

	/// <summary>The version of the calibration data model is not supported </summary>
	public const int Jl_ERR_CM_WR_DESCR_VERS = 8464;

	/// <summary>zero-motion in linear scan camera </summary>
	public const int Jl_ERR_CM_ZERO_MOTION = 8465;

	/// <summary>multi-camera and -calibobj not supported for all camera types </summary>
	public const int Jl_ERR_CM_MULTICAM_UNSP = 8466;

	/// <summary>incomplete data, required for legacy calibration </summary>
	public const int Jl_ERR_CM_INCMPLTE_DATA = 8467;

	/// <summary>Invalid file format for camera setup model </summary>
	public const int Jl_ERR_CSM_NO_DESCR_FIL = 8468;

	/// <summary>The version of the camera setup model is not supported </summary>
	public const int Jl_ERR_CSM_WR_DESCR_VER = 8469;

	/// <summary>full Vision-caltab descr'n required </summary>
	public const int Jl_ERR_CM_CALTAB_NOT_AV = 8470;

	/// <summary>invalid observation ID </summary>
	public const int Jl_ERR_CM_INVAL_OBSERID = 8471;

	/// <summary>Serialized item does not contain a valid camera setup model </summary>
	public const int Jl_ERR_CSM_NOSITEM = 8472;

	/// <summary>Serialized item does not contain a valid calibration data model </summary>
	public const int Jl_ERR_CM_NOSITEM = 8473;

	/// <summary>Invalid tool pose id </summary>
	public const int Jl_ERR_CM_INV_TOOLPOSID = 8474;

	/// <summary>Undefined tool pose </summary>
	public const int Jl_ERR_CM_UNDEFINED_TOO = 8475;

	/// <summary>Invalid calib data model type </summary>
	public const int Jl_ERR_CM_INVLD_MODL_TY = 8476;

	/// <summary>The camera setup model contains an uninitialized camera </summary>
	public const int Jl_ERR_CSM_UNINIT_CAM = 8477;

	/// <summary>The hand-eye algorithm failed to find a solution. </summary>
	public const int Jl_ERR_CM_NO_VALID_SOL = 8478;

	/// <summary>invalid observation pose </summary>
	public const int Jl_ERR_CM_INVAL_OBS_POSE = 8479;

	/// <summary>Not enough calibration object poses </summary>
	public const int Jl_ERR_CM_TOO_FEW_POSES = 8480;

	/// <summary>undefined camera type </summary>
	public const int Jl_ERR_CM_UNDEF_CAM_TYP = 8481;

	/// <summary>Num of image pairs does not correspond to num of disparity values </summary>
	public const int Jl_ERR_SM_INVLD_IMG_PAIRS_DISP_VAL = 8482;

	/// <summary>Invalid min/max disparity values </summary>
	public const int Jl_ERR_SM_INVLD_DISP_VAL = 8483;

	/// <summary>No camera pair set by set_stereo_model_image_pairs </summary>
	public const int Jl_ERR_SM_NO_IM_PAIR = 8484;

	/// <summary>No reconstructed point is visible for coloring </summary>
	public const int Jl_ERR_SM_NO_VIS_COLOR = 8485;

	/// <summary>No camera pair yields reconstructed points (please check parameters of disparity method or bounding box) </summary>
	public const int Jl_ERR_SM_NO_RECONSTRUCT = 8486;

	/// <summary>Partitioning of bounding box is too fine (please adapt the parameter 'resolution' or the bounding box)</summary>
	public const int Jl_ERR_SM_INVLD_BB_PARTITION = 8487;

	/// <summary>Invalid disparity range for binocular_disparity_ms method </summary>
	public const int Jl_ERR_SM_INVLD_DISP_RANGE = 8488;

	/// <summary>Invalid param for binoculuar method </summary>
	public const int Jl_ERR_SM_INVLD_BIN_PAR = 8489;

	/// <summary>invalid stereo model type </summary>
	public const int Jl_ERR_SM_INVLD_MODL_TY = 8490;

	/// <summary>stereo model is not in persistent mode </summary>
	public const int Jl_ERR_SM_NOT_PERSISTEN = 8491;

	/// <summary>invalid bounding box </summary>
	public const int Jl_ERR_SM_INVLD_BOU_BOX = 8492;

	/// <summary>stereo reconstruction: image sizes must correspond to camera setup </summary>
	public const int Jl_ERR_SR_INVLD_IMG_SIZ = 8493;

	/// <summary>bounding box is behind basis line </summary>
	public const int Jl_ERR_SR_BBOX_BHND_CAM = 8494;

	/// <summary>Ambiguous calibration: Please, recalibrate with improved input data!</summary>
	public const int Jl_ERR_CAL_AMBIGUOUS = 8495;

	/// <summary>Pose of calibration plate not determined </summary>
	public const int Jl_ERR_CAL_PCPND = 8496;

	/// <summary>Calibration failed: Please check your input data and calibrate again! </summary>
	public const int Jl_ERR_CAL_FAILED = 8497;

	/// <summary>No observation data supplied! </summary>
	public const int Jl_ERR_CAL_MISSING_DATA = 8498;

	/// <summary>The calibration object has to be seen at least once by every camera, if less than four cameras are used. </summary>
	public const int Jl_ERR_CAL_FEWER_FOUR = 8499;

	/// <summary>Invalid file format for template </summary>
	public const int Jl_ERR_NOAP = 8500;

	/// <summary>The version of the template is not supported </summary>
	public const int Jl_ERR_WPFV = 8501;

	/// <summary>Number of template points too small </summary>
	public const int Jl_ERR_NGTPTS = 8506;

	/// <summary>Template data can only be read by Vision XL </summary>
	public const int Jl_ERR_PDTL = 8507;

	/// <summary>Serialized item does not contain a valid NCC model </summary>
	public const int Jl_ERR_NCC_NOSITEM = 8508;

	/// <summary>Number of shape model points too small </summary>
	public const int Jl_ERR_NTPTS = 8510;

	/// <summary>Gray and color shape models mixed </summary>
	public const int Jl_ERR_CGSMM = 8511;

	/// <summary>Shape model data can only be read by Vision XL </summary>
	public const int Jl_ERR_SMTL = 8512;

	/// <summary>Shape model was not created from XLDs </summary>
	public const int Jl_ERR_SMNXLD = 8513;

	/// <summary>Serialized item does not contain a valid shape model </summary>
	public const int Jl_ERR_SM_NOSITEM = 8514;

	/// <summary>Shape model contour too near to clutter region </summary>
	public const int Jl_ERR_SM_CL_CONT = 8515;

	/// <summary>Shape model does not contain clutter parameters </summary>
	public const int Jl_ERR_SM_NO_CLUT = 8516;

	/// <summary>Shape models are not of the same clutter type </summary>
	public const int Jl_ERR_SM_SAME_CL = 8517;

	/// <summary>Shape model has an invalid clutter contrast </summary>
	public const int Jl_ERR_SM_WRONG_CLCO = 8518;

	/// <summary>Clutter region contains negative coordinates </summary>
	public const int Jl_ERR_SM_CL_NEG = 8519;

	/// <summary>Box finder: Unsupported generic parameter </summary>
	public const int Jl_ERR_FIND_BOX_UNSUP_GENPARAM = 8520;

	/// <summary>Initial components have different region types </summary>
	public const int Jl_ERR_COMP_DRT = 8530;

	/// <summary>Solution of ambiguous matches failed </summary>
	public const int Jl_ERR_COMP_SAMF = 8531;

	/// <summary>Computation of the incomplete gamma function not converged </summary>
	public const int Jl_ERR_IGF_NC = 8532;

	/// <summary>Too many nodes while computing the minimum spanning arborescence </summary>
	public const int Jl_ERR_MSA_TMN = 8533;

	/// <summary>Component training data can only be read by Vision XL </summary>
	public const int Jl_ERR_CTTL = 8534;

	/// <summary>Component model data can only be read by Vision XL </summary>
	public const int Jl_ERR_CMTL = 8535;

	/// <summary>Serialized item does not contain a valid component model </summary>
	public const int Jl_ERR_COMP_NOSITEM = 8536;

	/// <summary>Serialized item does not contain a valid component training result </summary>
	public const int Jl_ERR_TRAIN_COMP_NOSITEM = 8537;

	/// <summary>Size of the training image and the variation model differ </summary>
	public const int Jl_ERR_VARIATION_WS = 8540;

	/// <summary>Variation model has not been prepared for segmentation </summary>
	public const int Jl_ERR_VARIATION_PREP = 8541;

	/// <summary>Invalid variation model training mode </summary>
	public const int Jl_ERR_VARIATION_WRMD = 8542;

	/// <summary>Invalid file format for variation model </summary>
	public const int Jl_ERR_VARIATION_NOVF = 8543;

	/// <summary>The version of the variation model is not supported </summary>
	public const int Jl_ERR_VARIATION_WVFV = 8544;

	/// <summary>Training data has been cleared </summary>
	public const int Jl_ERR_VARIATION_TRDC = 8545;

	/// <summary>Serialized item does not contain a valid variation model </summary>
	public const int Jl_ERR_VARIATION_NOSITEM = 8546;

	/// <summary>No more measure objects available </summary>
	public const int Jl_ERR_MEASURE_NA = 8550;

	/// <summary>Measure object is not initialized </summary>
	public const int Jl_ERR_MEASURE_NI = 8551;

	/// <summary>Invalid measure object </summary>
	public const int Jl_ERR_MEASURE_OOR = 8552;

	/// <summary>Measure object is NULL </summary>
	public const int Jl_ERR_MEASURE_IS = 8553;

	/// <summary>Measure object has wrong image size </summary>
	public const int Jl_ERR_MEASURE_WS = 8554;

	/// <summary>Invalid file format for measure object </summary>
	public const int Jl_ERR_MEASURE_NO_MODEL_FILE = 8555;

	/// <summary>The version of the measure object is not supported </summary>
	public const int Jl_ERR_MEASURE_WRONG_VERSION = 8556;

	/// <summary>Measure object data can only be read by Vision XL </summary>
	public const int Jl_ERR_MEASURE_TL = 8557;

	/// <summary>Serialized item does not contain a valid measure object </summary>
	public const int Jl_ERR_MEASURE_NOSITEM = 8558;

	/// <summary>Metrology model is not initialized </summary>
	public const int Jl_ERR_METROLOGY_MODEL_NI = 8570;

	/// <summary>Invalid metrology object </summary>
	public const int Jl_ERR_METROLOGY_OBJECT_INVALID = 8572;

	/// <summary>Not enough valid measures for fitting the metrology object </summary>
	public const int Jl_ERR_METROLOGY_FIT_NOT_ENOUGH_MEASURES = 8573;

	/// <summary>Invalid file format for metrology model </summary>
	public const int Jl_ERR_METROLOGY_NO_MODEL_FILE = 8575;

	/// <summary>The version of the metrology model is not supported </summary>
	public const int Jl_ERR_METROLOGY_WRONG_VERSION = 8576;

	/// <summary>Fuzzy function is not set </summary>
	public const int Jl_ERR_METROLOGY_NO_FUZZY_FUNC = 8577;

	/// <summary>Serialized item does not contain a valid metrology model </summary>
	public const int Jl_ERR_METROLOGY_NOSITEM = 8578;

	/// <summary>Camera parameters are not set </summary>
	public const int Jl_ERR_METROLOGY_UNDEF_CAMPAR = 8579;

	/// <summary>Pose of the measurement plane is not set </summary>
	public const int Jl_ERR_METROLOGY_UNDEF_POSE = 8580;

	/// <summary>Mode of metrology model cannot be set since an object has already been added </summary>
	public const int Jl_ERR_METROLOGY_SET_MODE = 8581;

	/// <summary>If the pose of the metrology object has been set several times, the operator is not longer allowed </summary>
	public const int Jl_ERR_METROLOGY_OP_NOT_ALLOWED = 8582;

	/// <summary>All objects of a metrology model must have the same world pose and camera parameters. </summary>
	public const int Jl_ERR_METROLOGY_MULTI_POSE_CAM_PAR = 8583;

	/// <summary>Input type of metrology model does not correspond with the current input type </summary>
	public const int Jl_ERR_METROLOGY_WRONG_INPUT_MODE = 8584;

	/// <summary>Dynamic library could not be opened </summary>
	public const int Jl_ERR_DLOPEN = 8600;

	/// <summary>Dynamic library could not be closed </summary>
	public const int Jl_ERR_DLCLOSE = 8601;

	/// <summary>Symbol not found in dynamic library </summary>
	public const int Jl_ERR_DLLOOKUP = 8602;

	/// <summary>Interface library not * available </summary>
	public const int Jl_ERR_COMPONENT_NOT_INSTALLED = 8603;

	/// <summary>Not enough information for rad. calib. </summary>
	public const int Jl_ERR_EAD_CAL_NII = 8650;

	/// <summary>The version of the shape model result is not supported </summary>
	public const int Jl_ERR_WGSMFV = 8670;

	/// <summary>Restrict scale parameter outside the trained range </summary>
	public const int Jl_ERR_GSM_INVALID_RES_SCALE = 8671;

	/// <summary>Angle parameter outside the trained range </summary>
	public const int Jl_ERR_GSM_INVALID_ANGLE = 8672;

	/// <summary>Shape model needs training </summary>
	public const int Jl_ERR_GSM_NEEDS_TRAINING = 8673;

	/// <summary>contrast_high cannot be smaller than contrast_low </summary>
	public const int Jl_ERR_GSM_CONTRAST_HYS = 8674;

	/// <summary>Neither contrast_low nor contrast_high can be smaller than min_contrast </summary>
	public const int Jl_ERR_GSM_CONTRAST_MIN_CONTRAST = 8675;

	/// <summary>iso_scale_max cannot be smaller than iso_scale_min </summary>
	public const int Jl_ERR_GSM_ISO_SCALE_PAIR = 8676;

	/// <summary>scale_row_max cannot be smaller than scale_row_min </summary>
	public const int Jl_ERR_GSM_ANISO_SCALE_ROW = 8677;

	/// <summary>scale_column_max cannot be smaller than scale_column_min </summary>
	public const int Jl_ERR_GSM_ANISO_SCALE_COLUMN = 8678;

	/// <summary>Isotropic scaling not set </summary>
	public const int Jl_ERR_GSM_ISO_NOT_SET = 8679;

	/// <summary>Anisotropic scaling not set </summary>
	public const int Jl_ERR_GSM_ANISO_NOT_SET = 8680;

	/// <summary>No edge direction available to change shape matching metric </summary>
	public const int Jl_ERR_GSM_INVALID_METRIC_XLD = 8681;

	/// <summary>Shape models with the same identifier cannot be searched simultaneously </summary>
	public const int Jl_ERR_GSM_SAME_IDENTIFIER = 8682;

	/// <summary>Set parameters inconsistent with est. 'per_level' values </summary>
	public const int Jl_ERR_SM_INCONSISTENT_PER_LEVEL = 8683;

	/// <summary>Sample-based training failed </summary>
	public const int Jl_ERR_GSM_SAMPLE_TRAINING = 8684;

	/// <summary>Model setting does not allow the calculation of model point scores </summary>
	public const int Jl_ERR_GSM_POINT_SCORES = 8685;

	/// <summary>Model setting is not compatible with the set methods for sample-based training</summary>
	public const int Jl_ERR_GSM_SET_SAMPLE_TRAINING = 8686;

	/// <summary>Wrong number of modules </summary>
	public const int Jl_ERR_BAR_WNOM = 8701;

	/// <summary>Wrong number of elements </summary>
	public const int Jl_ERR_BAR_WNOE = 8702;

	/// <summary>Unknown character (for this code) </summary>
	public const int Jl_ERR_BAR_UNCHAR = 8703;

	/// <summary>Wrong name for attribute in barcode descriptor </summary>
	public const int Jl_ERR_BAR_WRONGDESCR = 8705;

	/// <summary>Wrong thickness of element </summary>
	public const int Jl_ERR_BAR_EL_LENGTH = 8706;

	/// <summary>No region found </summary>
	public const int Jl_ERR_BAR_NO_REG = 8707;

	/// <summary>Wrong type of bar code </summary>
	public const int Jl_ERR_BAR_WRONGCODE = 8708;

	/// <summary>Internal error in bar code reader </summary>
	public const int Jl_ERR_BAR_INTERNAL = 8709;

	/// <summary>Candidate does not contain a decoded scanline </summary>
	public const int Jl_ERR_BAR_NO_DECODED_SCANLINE = 8710;

	/// <summary>Empty model list </summary>
	public const int Jl_ERR_BC_EMPTY_MODEL_LIST = 8721;

	/// <summary>Training cannot be done for multiple bar code types </summary>
	public const int Jl_ERR_BC_TRAIN_ONLY_SINGLE = 8722;

	/// <summary>Cannot get bar code type specific parameter with get_bar_code_param. Use get_bar_code_param_specific </summary>
	public const int Jl_ERR_BC_GET_SPECIFIC = 8723;

	/// <summary>Cannot get this object for multiple bar code types. Try again with single bar code type </summary>
	public const int Jl_ERR_BC_GET_OBJ_MULTI = 8724;

	/// <summary>Wrong binary (file) format </summary>
	public const int Jl_ERR_BC_WR_FILE_FORMAT = 8725;

	/// <summary>Wrong version of binary file </summary>
	public const int Jl_ERR_BC_WR_FILE_VERS = 8726;

	/// <summary>The model must be in persistency mode to deliver the required object/result </summary>
	public const int Jl_ERR_BC_NOT_PERSISTANT = 8727;

	/// <summary>Incorrect index of scanline's gray values</summary>
	public const int Jl_ERR_BC_GRAY_OUT_OF_RANGE = 8728;

	/// <summary>Neither find_bar_code nor decode_bar_code_rectanlge2 has been called in 'persistent' mode on this model </summary>
	public const int Jl_ERR_NO_PERSISTENT_OP_CALL = 8729;

	/// <summary>The super-resolution algorithm has been aborted </summary>
	public const int Jl_ERR_BC_ZOOMED_ABORTED = 8730;

	/// <summary>SRB: Invalid input data. </summary>
	public const int Jl_ERR_BC_ZOOMED_INVALID_INPUT = 8731;

	/// <summary>Invalid input detected for barcode normalized cross correlation </summary>
	public const int Jl_ERR_BC_XCORR_INVALID_INPUT = 8740;

	/// <summary>Too many bad rows found during barcode normalized cross correlation </summary>
	public const int Jl_ERR_BC_XCORR_TOO_MANY_BAD_ROWS = 8741;

	/// <summary>No correlation found during barcode normalized cross correlation </summary>
	public const int Jl_ERR_BC_XCORR_NO_CORRELATION = 8742;

	/// <summary>Invalid GS1 syntax dictionary </summary>
	public const int Jl_ERR_INVALID_SYNTAX_DICTIONARY = 8743;

	/// <summary>Specified code type is not supported </summary>
	public const int Jl_ERR_BAR2D_UNKNOWN_TYPE = 8800;

	/// <summary>Wrong foreground specified </summary>
	public const int Jl_ERR_BAR2D_WRONG_FOREGROUND = 8801;

	/// <summary>Wrong matrix size specified </summary>
	public const int Jl_ERR_BAR2D_WRONG_SIZE = 8802;

	/// <summary>Wrong symbol shape specified </summary>
	public const int Jl_ERR_BAR2D_WRONG_SHAPE = 8803;

	/// <summary>Wrong generic parameter name </summary>
	public const int Jl_ERR_BAR2D_WRONG_PARAM_NAME = 8804;

	/// <summary>Wrong generic parameter value </summary>
	public const int Jl_ERR_BAR2D_WRONG_PARAM_VAL = 8805;

	/// <summary>Wrong symbol printing mode </summary>
	public const int Jl_ERR_BAR2D_WRONG_MODE = 8806;

	/// <summary>Symbol region too near to image border </summary>
	public const int Jl_ERR_BAR2D_SYMBOL_ON_BORDER = 8807;

	/// <summary>No rectangular module boundings found </summary>
	public const int Jl_ERR_BAR2D_MODULE_CONT_NUM = 8808;

	/// <summary>Couldn't identify symbol finder </summary>
	public const int Jl_ERR_BAR2D_SYMBOL_FINDER = 8809;

	/// <summary>Symbol region with wrong dimension </summary>
	public const int Jl_ERR_BAR2D_SYMBOL_DIMENSION = 8810;

	/// <summary>Classification failed </summary>
	public const int Jl_ERR_BAR2D_CLASSIF_FAILED = 8811;

	/// <summary>Decoding failed </summary>
	public const int Jl_ERR_BAR2D_DECODING_FAILED = 8812;

	/// <summary>Reader programming not supported </summary>
	public const int Jl_ERR_BAR2D_DECODING_READER = 8813;

	/// <summary>General 2d data code error </summary>
	public const int Jl_ERR_DC2D_GENERAL = 8820;

	/// <summary>Corrupt signature of 2d data code handle </summary>
	public const int Jl_ERR_DC2D_BROKEN_SIGN = 8821;

	/// <summary>Invalid 2d data code handle </summary>
	public const int Jl_ERR_DC2D_INVALID_HANDLE = 8822;

	/// <summary>List of 2d data code models is empty </summary>
	public const int Jl_ERR_DC2D_EMPTY_MODEL_LIST = 8823;

	/// <summary>Access to uninitialized (or not persistent) internal data </summary>
	public const int Jl_ERR_DC2D_NOT_INITIALIZED = 8824;

	/// <summary>Invalid 'Candidate' parameter </summary>
	public const int Jl_ERR_DC2D_INVALID_CANDIDATE = 8825;

	/// <summary>It's not possible to return more than one parameter for several candidates </summary>
	public const int Jl_ERR_DC2D_INDEX_PARNUM = 8826;

	/// <summary>One of the parameters returns several values and has to be used exclusively for a single candidate </summary>
	public const int Jl_ERR_DC2D_EXCLUSIV_PARAM = 8827;

	/// <summary>Parameter for default settings must be the first in the parameter list </summary>
	public const int Jl_ERR_DC2D_DEF_SET_NOT_FIRST = 8828;

	/// <summary>Unexpected 2d data code error </summary>
	public const int Jl_ERR_DC2D_INTERNAL_UNEXPECTED = 8829;

	/// <summary>Invalid parameter value </summary>
	public const int Jl_ERR_DC2D_WRONG_PARAM_VALUE = 8830;

	/// <summary>Unknown parameter name </summary>
	public const int Jl_ERR_DC2D_WRONG_PARAM_NAME = 8831;

	/// <summary>Invalid 'polarity' </summary>
	public const int Jl_ERR_DC2D_WRONG_POLARITY = 8832;

	/// <summary>Invalid 'symbol_shape' </summary>
	public const int Jl_ERR_DC2D_WRONG_SYMBOL_SHAPE = 8833;

	/// <summary>Invalid symbol size </summary>
	public const int Jl_ERR_DC2D_WRONG_SYMBOL_SIZE = 8834;

	/// <summary>Invalid module size </summary>
	public const int Jl_ERR_DC2D_WRONG_MODULE_SIZE = 8835;

	/// <summary>Invalid 'module_shape' </summary>
	public const int Jl_ERR_DC2D_WRONG_MODULE_SHAPE = 8836;

	/// <summary>Invalid 'orientation' </summary>
	public const int Jl_ERR_DC2D_WRONG_ORIENTATION = 8837;

	/// <summary>Invalid 'contrast_min' </summary>
	public const int Jl_ERR_DC2D_WRONG_CONTRAST = 8838;

	/// <summary>Invalid 'measure_thresh' </summary>
	public const int Jl_ERR_DC2D_WRONG_MEAS_THRESH = 8839;

	/// <summary>Invalid 'alt_measure_red' </summary>
	public const int Jl_ERR_DC2D_WRONG_ALT_MEAS_RED = 8840;

	/// <summary>Invalid 'slant_max' </summary>
	public const int Jl_ERR_DC2D_WRONG_SLANT = 8841;

	/// <summary>Invalid 'L_dist_max' </summary>
	public const int Jl_ERR_DC2D_WRONG_L_DIST = 8842;

	/// <summary>Invalid 'L_length_min' </summary>
	public const int Jl_ERR_DC2D_WRONG_L_LENGTH = 8843;

	/// <summary>Invalid module gap </summary>
	public const int Jl_ERR_DC2D_WRONG_GAP = 8844;

	/// <summary>Invalid 'default_parameters' </summary>
	public const int Jl_ERR_DC2D_WRONG_DEF_SET = 8845;

	/// <summary>Invalid 'back_texture' </summary>
	public const int Jl_ERR_DC2D_WRONG_TEXTURED = 8846;

	/// <summary>Invalid 'mirrored' </summary>
	public const int Jl_ERR_DC2D_WRONG_MIRRORED = 8847;

	/// <summary>Invalid 'classificator' </summary>
	public const int Jl_ERR_DC2D_WRONG_CLASSIFICATOR = 8848;

	/// <summary>Invalid 'persistence' </summary>
	public const int Jl_ERR_DC2D_WRONG_PERSISTENCE = 8849;

	/// <summary>Invalid model type </summary>
	public const int Jl_ERR_DC2D_WRONG_MODEL_TYPE = 8850;

	/// <summary>Invalid 'module_roi_part' </summary>
	public const int Jl_ERR_DC2D_WRONG_MOD_ROI_PART = 8851;

	/// <summary>Invalid 'finder_pattern_tolerance' </summary>
	public const int Jl_ERR_DC2D_WRONG_FP_TOLERANCE = 8852;

	/// <summary>Invalid 'mod_aspect_max' </summary>
	public const int Jl_ERR_DC2D_WRONG_MOD_ASPECT = 8853;

	/// <summary>Invalid 'small_modules_robustness' </summary>
	public const int Jl_ERR_DC2D_WRONG_SM_ROBUSTNESS = 8854;

	/// <summary>Invalid 'contrast_tolerance' </summary>
	public const int Jl_ERR_DC2D_WRONG_CONTRAST_TOL = 8855;

	/// <summary>Invalid 'alternating_pattern_tolerance' </summary>
	public const int Jl_ERR_DC2D_WRONG_AP_TOLERANCE = 8856;

	/// <summary>Invalid 'deformation_tolerance' </summary>
	public const int Jl_ERR_DC2D_WRONG_DEFORM_TOL = 8857;

	/// <summary>Invalid header in 2d data code model file </summary>
	public const int Jl_ERR_DC2D_READ_HEAD_FORMAT = 8860;

	/// <summary>Invalid code signature in 2d data code model file </summary>
	public const int Jl_ERR_DC2D_READ_HEAD_SIGN = 8861;

	/// <summary>Corrupted line in 2d data code model file </summary>
	public const int Jl_ERR_DC2D_READ_LINE_FORMAT = 8862;

	/// <summary>Invalid module aspect ratio </summary>
	public const int Jl_ERR_DC2D_WRONG_MODULE_ASPECT = 8863;

	/// <summary>wrong number of layers </summary>
	public const int Jl_ERR_DC2D_WRONG_LAYER_NUM = 8864;

	/// <summary>wrong data code model version </summary>
	public const int Jl_ERR_DCD_READ_WRONG_VERSION = 8865;

	/// <summary>Serialized item does not contain a valid 2D data code model </summary>
	public const int Jl_ERR_DC2D_NOSITEM = 8866;

	/// <summary>Wrong binary (file) format </summary>
	public const int Jl_ERR_DC2D_WR_FILE_FORMAT = 8867;

	/// <summary>Parameter only available with detection_method='deep_learning' </summary>
	public const int Jl_ERR_DC2D_PARAM_ONLY_AVAILABLE_WITH_DL = 8868;

	/// <summary>Invalid 'alternating_pattern_tolerance_ql' </summary>
	public const int Jl_ERR_DC2D_WRONG_AP_TOLERANCE_QL = 8869;

	/// <summary>Invalid parameter value </summary>
	public const int Jl_ERR_SM3D_WRONG_PARAM_NAME = 8900;

	/// <summary>Invalid 'num_levels' </summary>
	public const int Jl_ERR_SM3D_WRONG_NUM_LEVELS = 8901;

	/// <summary>Invalid 'optimization' </summary>
	public const int Jl_ERR_SM3D_WRONG_OPTIMIZATION = 8902;

	/// <summary>Invalid 'metric' </summary>
	public const int Jl_ERR_SM3D_WRONG_METRIC = 8903;

	/// <summary>Invalid 'min_face_angle' </summary>
	public const int Jl_ERR_SM3D_WRONG_MIN_FACE_ANGLE = 8904;

	/// <summary>Invalid 'min_size' </summary>
	public const int Jl_ERR_SM3D_WRONG_MIN_SIZE = 8905;

	/// <summary>Invalid 'model_tolerance' </summary>
	public const int Jl_ERR_SM3D_WRONG_MODEL_TOLERANCE = 8906;

	/// <summary>Invalid 'fast_pose_refinment'</summary>
	public const int Jl_ERR_SM3D_WRONG_FAST_POSE_REF = 8907;

	/// <summary>Invalid 'lowest_model_level'</summary>
	public const int Jl_ERR_SM3D_WRONG_LOWEST_MODEL_LEVEL = 8908;

	/// <summary>Invalid 'part_size'</summary>
	public const int Jl_ERR_SM3D_WRONG_PART_SIZE = 8909;

	/// <summary>The projected model is too large (increase the value for DistMin or the image size in CamParam) </summary>
	public const int Jl_ERR_SM3D_PROJECTION_TOO_LARGE = 8910;

	/// <summary>Invalid 'opengl_accuracy'</summary>
	public const int Jl_ERR_SM3D_WRONG_OPENGL_ACCURACY = 8911;

	/// <summary>Invalid 'recompute_score'</summary>
	public const int Jl_ERR_SM3D_WRONG_RECOMPUTE_SCORE = 8913;

	/// <summary>Invalid 'longitude_min' </summary>
	public const int Jl_ERR_SM3D_WRONG_LON_MIN = 8920;

	/// <summary>Invalid 'longitude_max' </summary>
	public const int Jl_ERR_SM3D_WRONG_LON_MAX = 8921;

	/// <summary>Invalid 'latitude_min </summary>
	public const int Jl_ERR_SM3D_WRONG_LAT_MIN = 8922;

	/// <summary>Invalid 'latitude_max' </summary>
	public const int Jl_ERR_SM3D_WRONG_LAT_MAX = 8923;

	/// <summary>Invalid 'cam_roll_min' </summary>
	public const int Jl_ERR_SM3D_WRONG_ROL_MIN = 8924;

	/// <summary>Invalid 'cam_roll_max' </summary>
	public const int Jl_ERR_SM3D_WRONG_ROL_MAX = 8925;

	/// <summary>Invalid 'dist_min' </summary>
	public const int Jl_ERR_SM3D_WRONG_DIST_MIN = 8926;

	/// <summary>Invalid 'dist_max' </summary>
	public const int Jl_ERR_SM3D_WRONG_DIST_MAX = 8927;

	/// <summary>Invalid 'num_matches' </summary>
	public const int Jl_ERR_SM3D_WRONG_NUM_MATCHES = 8928;

	/// <summary>Invalid 'max_overlap' </summary>
	public const int Jl_ERR_SM3D_WRONG_MAX_OVERLAP = 8929;

	/// <summary>Invalid 'pose_refinement' </summary>
	public const int Jl_ERR_SM3D_WRONG_POSE_REFINEMENT = 8930;

	/// <summary>Invalid 'cov_pose_mode' </summary>
	public const int Jl_ERR_SM3D_WRONG_COV_POSE_MODE = 8931;

	/// <summary>In. 'outlier_suppression' </summary>
	public const int Jl_ERR_SM3D_WRONG_OUTLIER_SUP = 8932;

	/// <summary>Invalid 'border_model' </summary>
	public const int Jl_ERR_SM3D_WRONG_BORDER_MODEL = 8933;

	/// <summary>Pose is not well-defined </summary>
	public const int Jl_ERR_SM3D_UNDEFINED_POSE = 8940;

	/// <summary>Invalid file format for 3D shape model </summary>
	public const int Jl_ERR_SM3D_NO_SM3D_FILE = 8941;

	/// <summary>The version of the 3D shape model is not supported </summary>
	public const int Jl_ERR_SM3D_WRONG_FILE_VERSION = 8942;

	/// <summary>3D shape model can only be read by Vision XL </summary>
	public const int Jl_ERR_SM3D_MTL = 8943;

	/// <summary>3D object model does not contain any faces </summary>
	public const int Jl_ERR_SM3D_NO_OM3D_FACES = 8944;

	/// <summary>Serialized item does not contain a valid 3D shape model </summary>
	public const int Jl_ERR_SM3D_NOSITEM = 8945;

	/// <summary>Invalid 'union_adjacent_contours' </summary>
	public const int Jl_ERR_SM3D_WRONG_UNION_ADJACENT_CONTOURS = 8946;

	/// <summary>Pose estimation model contains insufficient information </summary>
	public const int Jl_ERR_DM3D_NO3DPOSEEST = 8947;

	/// <summary>Invalid file format for descriptor model </summary>
	public const int Jl_ERR_DESCR_NODESCRFILE = 8960;

	/// <summary>The version of the descriptor model is not supported </summary>
	public const int Jl_ERR_DESCR_WRDESCRVERS = 8961;

	/// <summary>Invalid 'radius' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_CIRC_RADIUS = 8962;

	/// <summary>Invalid 'check_neighbor' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_CHECK_NEIGH = 8963;

	/// <summary>Invalid 'min_check_neighbor_diff' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MIN_CHECK_NEIGH = 8964;

	/// <summary>Invalid 'min_score' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MIN_SCORE = 8965;

	/// <summary>Invalid 'sigma_grad' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_SIGMAGRAD = 8966;

	/// <summary>Invalid 'sigma_smooth' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_SIGMAINT = 8967;

	/// <summary>Invalid 'alpha' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_ALPHA = 8968;

	/// <summary>Invalid 'threshold' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_THRESHOLD = 8969;

	/// <summary>Invalid 'depth' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_DEPTH = 8970;

	/// <summary>Invalid 'number_trees' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_TREES = 8971;

	/// <summary>Invalid 'min_score_descr' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MIN_SCORE_DESCR = 8972;

	/// <summary>Invalid 'patch_size' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_PATCH_SIZE = 8973;

	/// <summary>Invalid 'tilt' </summary>
	public const int Jl_ERR_DM_WRONG_TILT = 8974;

	/// <summary>Invalid 'guided_matching' </summary>
	public const int Jl_ERR_DM_WRONG_PAR_GUIDE = 8975;

	/// <summary>Invalid 'subpix' </summary>
	public const int Jl_ERR_DM_WRONG_PAR_SUBPIX = 8976;

	/// <summary>Too few feature points can be found </summary>
	public const int Jl_ERR_DM_TOO_FEW_POINTS = 8977;

	/// <summary>Invalid 'min_rot' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MINROT = 8978;

	/// <summary>Invalid 'max_rot' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MAXROT = 8979;

	/// <summary>Invalid 'min_scale' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MINSCALE = 8980;

	/// <summary>Invalid 'max_scale' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MAXSCALE = 8981;

	/// <summary>Invalid 'mask_size_grd' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MASKSIZEGRD = 8982;

	/// <summary>Invalid 'mask_size_smooth' </summary>
	public const int Jl_ERR_DM_WRONG_NUM_MASKSIZESMOOTH = 8983;

	/// <summary>Model broken </summary>
	public const int Jl_ERR_BROKEN_MODEL = 8984;

	/// <summary>Invalid 'descriptor_type' </summary>
	public const int Jl_ERR_DM_WRONG_DESCR_TYPE = 8985;

	/// <summary>Invalid 'matcher' </summary>
	public const int Jl_ERR_DM_WRONG_PAR_MATCHER = 8986;

	/// <summary>Too many point classes - cannot be written to file </summary>
	public const int Jl_ERR_DM_TOO_MANY_CLASSES = 8987;

	/// <summary>Serialized item does not contain a valid descriptor model </summary>
	public const int Jl_ERR_DESCR_NOSITEM = 8988;

	/// <summary>Function not implemented on this machine </summary>
	public const int Jl_ERR_NOT_IMPL = 9000;

	/// <summary>Image to process has wrong gray value type </summary>
	public const int Jl_ERR_WIT = 9001;

	/// <summary>Wrong image component </summary>
	public const int Jl_ERR_WIC = 9002;

	/// <summary>Undefined gray values </summary>
	public const int Jl_ERR_UNDI = 9003;

	/// <summary>Wrong image format for operation (too big or too small) </summary>
	public const int Jl_ERR_WIS = 9004;

	/// <summary>Wrong number of image components for image output </summary>
	public const int Jl_ERR_WCN = 9005;

	/// <summary>String is too long (max. 1024 characters) </summary>
	public const int Jl_ERR_STRTL = 9006;

	/// <summary>Wrong pixel type for this operation </summary>
	public const int Jl_ERR_WITFO = 9007;

	/// <summary>Operation not realized yet for this pixel type </summary>
	public const int Jl_ERR_NIIT = 9008;

	/// <summary>Image is no color image with three channels </summary>
	public const int Jl_ERR_NOCIMA = 9009;

	/// <summary>Image acquisition devices are not supported in the demo version </summary>
	public const int Jl_ERR_DEMO_NOFG = 9010;

	/// <summary>Packages are not supported in the demo version </summary>
	public const int Jl_ERR_DEMO_NOPA = 9011;

	/// <summary>Internal Error: Unknown value</summary>
	public const int Jl_ERR_IEUNKV = 9020;

	/// <summary>Wrong parameter for this operation </summary>
	public const int Jl_ERR_WPFO = 9021;

	/// <summary>Image domain too small </summary>
	public const int Jl_ERR_IDTS = 9022;

	/// <summary>Draw operator has been canceled </summary>
	public const int Jl_ERR_CNCLDRW = 9023;

	/// <summary>Error during matching of regular * expression </summary>
	public const int Jl_ERR_REGEX_MATCH = 9024;

	/// <summary>Operator is not available in the student version of Vision </summary>
	public const int Jl_ERR_STUD_OPNA = 9050;

	/// <summary>Packages are not available in the student version of Vision </summary>
	public const int Jl_ERR_STUD_PANA = 9051;

	/// <summary>The selected image acquisition device is not available in the student version of Vision</summary>
	public const int Jl_ERR_STUD_FGNA = 9052;

	/// <summary>No data points available </summary>
	public const int Jl_ERR_NDPA = 9053;

	/// <summary>Object type is not supported. </summary>
	public const int Jl_ERR_WR_OBJ_TYPE = 9054;

	/// <summary>Operator is disabled. </summary>
	public const int Jl_ERR_OP_DISABLED = 9055;

	/// <summary>Too many unknown variables in linear equation </summary>
	public const int Jl_ERR_TMU = 9100;

	/// <summary>No (unique) solution for the linear equation </summary>
	public const int Jl_ERR_NUS = 9101;

	/// <summary>Too little equations in linear equation </summary>
	public const int Jl_ERR_NEE = 9102;

	/// <summary>Points do not define a line </summary>
	public const int Jl_ERR_PDDL = 9150;

	/// <summary>Matrix is not invertible </summary>
	public const int Jl_ERR_MNI = 9200;

	/// <summary>Singular value decomposition did not converge </summary>
	public const int Jl_ERR_SVD_CNVRG = 9201;

	/// <summary>Matrix has too few rows for singular value partition </summary>
	public const int Jl_ERR_SVD_FEWROW = 9202;

	/// <summary>Eigenvalue computation did not converge </summary>
	public const int Jl_ERR_TQLI_CNVRG = 9203;

	/// <summary>Eigenvalue computation did not converge </summary>
	public const int Jl_ERR_JACOBI_CNVRG = 9204;

	/// <summary>Matrix is singular </summary>
	public const int Jl_ERR_MATRIX_SING = 9205;

	/// <summary>Function matching did not converge </summary>
	public const int Jl_ERR_MATCH_CNVRG = 9206;

	/// <summary>Input matrix undefined </summary>
	public const int Jl_ERR_MAT_UNDEF = 9207;

	/// <summary>Input matrix with wrong dimension </summary>
	public const int Jl_ERR_MAT_WDIM = 9208;

	/// <summary>Input matrix is not quadratic </summary>
	public const int Jl_ERR_MAT_NSQR = 9209;

	/// <summary>Matrix operation failed </summary>
	public const int Jl_ERR_MAT_FAIL = 9210;

	/// <summary>Matrix is not positive definite </summary>
	public const int Jl_ERR_MAT_NPD = 9211;

	/// <summary>Matrix element division by 0 </summary>
	public const int Jl_ERR_MAT_DBZ = 9212;

	/// <summary>Matrix is not an upper triangular matrix </summary>
	public const int Jl_ERR_MAT_NUT = 9213;

	/// <summary>Matrix is not a lower triangular matrix </summary>
	public const int Jl_ERR_MAT_NLT = 9214;

	/// <summary>Matrix element is negative </summary>
	public const int Jl_ERR_MAT_NEG = 9215;

	/// <summary>Matrix file: Invalid character </summary>
	public const int Jl_ERR_MAT_UNCHAR = 9216;

	/// <summary>Matrix file: matrix incomplete </summary>
	public const int Jl_ERR_MAT_NOT_COMPLETE = 9217;

	/// <summary>Invalid file format for matrix </summary>
	public const int Jl_ERR_MAT_READ = 9218;

	/// <summary>Resulting matrix has complex values </summary>
	public const int Jl_ERR_MAT_COMPLEX = 9219;

	/// <summary>Wrong value in matrix of exponents </summary>
	public const int Jl_ERR_WMATEXP = 9220;

	/// <summary>The version of the matrix is not supported </summary>
	public const int Jl_ERR_MAT_WRONG_VERSION = 9221;

	/// <summary>Serialized item does not contain a valid matrix </summary>
	public const int Jl_ERR_MAT_NOSITEM = 9222;

	/// <summary>Internal Error: Wrong Node </summary>
	public const int Jl_ERR_WNODE = 9230;

	/// <summary>Inconsistent red black tree </summary>
	public const int Jl_ERR_CMP_INCONSISTENT = 9231;

	/// <summary>Internal error </summary>
	public const int Jl_ERR_LAPACK_PAR = 9250;

	/// <summary>Number of points too small </summary>
	public const int Jl_ERR_STRI_NPNT = 9260;

	/// <summary>First 3 points are collinear </summary>
	public const int Jl_ERR_STRI_COLL = 9261;

	/// <summary>Identical points in triangulation </summary>
	public const int Jl_ERR_STRI_IDPNT = 9262;

	/// <summary>Array not allocated large enough </summary>
	public const int Jl_ERR_STRI_NALLOC = 9263;

	/// <summary>Triangle is degenerate </summary>
	public const int Jl_ERR_STRI_DEGEN = 9264;

	/// <summary>Inconsistent triangulation </summary>
	public const int Jl_ERR_STRI_ITRI = 9265;

	/// <summary>Self-intersecting polygon </summary>
	public const int Jl_ERR_STRI_SELFINT = 9266;

	/// <summary>Inconsistent polygon data </summary>
	public const int Jl_ERR_STRI_INCONS = 9267;

	/// <summary>Ambiguous great circle arc intersection </summary>
	public const int Jl_ERR_STRI_AMBINT = 9268;

	/// <summary>Ambiguous great circle arc </summary>
	public const int Jl_ERR_STRI_AMBARC = 9269;

	/// <summary>Illegal parameter </summary>
	public const int Jl_ERR_STRI_ILLPAR = 9270;

	/// <summary>Not enough points for planar triangular meshing </summary>
	public const int Jl_ERR_TRI_NPNT = 9280;

	/// <summary>The first three points of the triangular meshing are collinear </summary>
	public const int Jl_ERR_TRI_COLL = 9281;

	/// <summary>Planar triangular meshing contains identical input points </summary>
	public const int Jl_ERR_TRI_IDPNT = 9282;

	/// <summary>Invalid points for planar triangular meshing </summary>
	public const int Jl_ERR_TRI_IDPNTIN = 9283;

	/// <summary>Internal error: allocated array too small for planar triangular meshing </summary>
	public const int Jl_ERR_TRI_NALLOC = 9284;

	/// <summary>Internal error: planar triangular meshing inconsistent </summary>
	public const int Jl_ERR_TRI_ITRI = 9285;

	/// <summary>Node index outside triangulation range </summary>
	public const int Jl_ERR_TRI_OUTR = 9286;

	/// <summary>Local inconsistencies for all points with valid neighbors (parameters only allow few valid neighborhoods or point cloud not subsampled) </summary>
	public const int Jl_ERR_TRI_LOCINC = 9290;

	/// <summary>Eye point and reference point coincide </summary>
	public const int Jl_ERR_WSPVP = 9300;

	/// <summary>Real part of the dual quaternion has length 0 </summary>
	public const int Jl_ERR_DQ_ZERO_NORM = 9310;

	/// <summary>Timeout occurred </summary>
	public const int Jl_ERR_TIMEOUT = 9400;

	/// <summary>Invalid 'timeout' </summary>
	public const int Jl_ERR_WRONG_TIMEOUT = 9401;

	/// <summary>Invalid 'part_size' </summary>
	public const int Jl_ERR_DEFORM_WRONG_NUM_CLUSTER = 9450;

	/// <summary>Invalid 'min_size' </summary>
	public const int Jl_ERR_DEFORM_WRONG_NUM_MIN_SIZE = 9451;

	/// <summary>Invalid number of least-squares iterations </summary>
	public const int Jl_ERR_DEFORM_WRONG_NUM_LSQ = 9452;

	/// <summary>Invalid 'angle_step' </summary>
	public const int Jl_ERR_DEFORM_WRONG_ANGLE_STEP = 9453;

	/// <summary>Invalid 'scale_r_step' </summary>
	public const int Jl_ERR_DEFORM_WRONG_SCALE_R_STEP = 9454;

	/// <summary>Invalid 'scale_c_step' </summary>
	public const int Jl_ERR_DEFORM_WRONG_SCALE_C_STEP = 9455;

	/// <summary>Invalid 'max_angle_distortion' </summary>
	public const int Jl_ERR_DEFORM_WRONG_MAX_ANGLE = 9456;

	/// <summary>Invalid 'max_aniso_scale_distortion' </summary>
	public const int Jl_ERR_DEFORM_WRONG_MAX_ANISO = 9457;

	/// <summary>Invalid 'min_size' </summary>
	public const int Jl_ERR_DEFORM_WRONG_MIN_SIZE = 9458;

	/// <summary>Invalid 'cov_pose_mode' </summary>
	public const int Jl_ERR_DEFORM_WRONG_COV_POSE_MODE = 9459;

	/// <summary>Model contains no calibration information </summary>
	public const int Jl_ERR_DEFORM_NO_CALIBRATION_INFO = 9460;

	/// <summary>Generic parameter name does not exist </summary>
	public const int Jl_ERR_DEFORM_WRONG_PARAM_NAME = 9461;

	/// <summary>camera has different resolution than image </summary>
	public const int Jl_ERR_DEFORM_IMAGE_TO_CAMERA_DIFF = 9462;

	/// <summary>Invalid file format for deformable model </summary>
	public const int Jl_ERR_DEFORM_NO_MODEL_IN_FILE = 9463;

	/// <summary>The version of the deformable model is not supported </summary>
	public const int Jl_ERR_DEFORM_WRONG_VERSION = 9464;

	/// <summary>Invalid 'deformation_smoothness'</summary>
	public const int Jl_ERR_DEFORM_WRONG_SMOOTH_DEFORM = 9465;

	/// <summary>Invalid 'expand_border' </summary>
	public const int Jl_ERR_DEFORM_WRONG_EXPAND_BORDER = 9466;

	/// <summary>Model origin outside of axis-aligned bounding rectangle of template region </summary>
	public const int Jl_ERR_DEFORM_ORIGIN_OUTSIDE_TEMPLATE = 9467;

	/// <summary>Serialized item does not contain a valid deformable model </summary>
	public const int Jl_ERR_DEFORM_NOSITEM = 9468;

	/// <summary>Estimation of viewpose failed </summary>
	public const int Jl_ERR_VIEW_ESTIM_FAIL = 9499;

	/// <summary>Object model has no points </summary>
	public const int Jl_ERR_SFM_NO_POINTS = 9500;

	/// <summary>Object model has no faces </summary>
	public const int Jl_ERR_SFM_NO_FACES = 9501;

	/// <summary>Object model has no normals </summary>
	public const int Jl_ERR_SFM_NO_NORMALS = 9502;

	/// <summary>3D surface model not trained for calculating view-based score </summary>
	public const int Jl_ERR_SFM_NO_VISIBILITY = 9503;

	/// <summary>3D surface model not trained for edge-supported matching </summary>
	public const int Jl_ERR_SFM_NO_3D_EDGES = 9504;

	/// <summary>Invalid file format for 3D surface model </summary>
	public const int Jl_ERR_SFM_NO_SFM_FILE = 9506;

	/// <summary>The version of the 3D surface model is not supported </summary>
	public const int Jl_ERR_SFM_WRONG_FILE_VERSION = 9507;

	/// <summary>Serialized item does not contain a valid 3D surface model </summary>
	public const int Jl_ERR_SFM_NOSITEM = 9508;

	/// <summary>Poses generate too many symmetries </summary>
	public const int Jl_ERR_SFM_TOO_MANY_SYMMS = 9509;

	/// <summary>Invalid 3D file </summary>
	public const int Jl_ERR_OM3D_INVALID_FILE = 9510;

	/// <summary>Invalid 3D Object Model </summary>
	public const int Jl_ERR_OM3D_INVALID_MODEL = 9511;

	/// <summary>Unknown 3D file type </summary>
	public const int Jl_ERR_OM3D_UNKNOWN_FILE_TYPE = 9512;

	/// <summary>The version of the 3D object model is not supported </summary>
	public const int Jl_ERR_OM3D_WRONG_FILE_VERSION = 9513;

	/// <summary>Required attribute is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB = 9514;

	/// <summary>Required attribute point_coord is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_V_COORD = 9515;

	/// <summary>Required attribute point_normal is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_V_NORMALS = 9516;

	/// <summary>Required attribute face_triangle is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_F_TRIANGLES = 9517;

	/// <summary>Required attribute line_array is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_F_LINES = 9518;

	/// <summary>Required attribute f_trineighb is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_F_TRINEIGB = 9519;

	/// <summary>Required attribute face_polygon is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_F_POLYGONS = 9520;

	/// <summary>Required attribute xyz_mapping is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_V_2DMAP = 9521;

	/// <summary>Required attribute o_primitive is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_O_PRIMITIVE = 9522;

	/// <summary>Required attribute shape_model is missing </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_SHAPE_MODEL = 9523;

	/// <summary>Required extended attribute missing in 3D object model </summary>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_EXTENDED = 9524;

	/// <summary>Serialized item does not contain a valid 3D object model </summary>
	public const int Jl_ERR_OM3D_NOSITEM = 9525;

	/// <summary>Primitive in 3D object model has no extended data </summary>
	public const int Jl_ERR_OM3D_MISSING_O_PRIMITIVE_EXTENSION = 9526;

	/// <summary>Operation invalid, 3D object model already contains triangles </summary>
	public const int Jl_ERR_OM3D_CONTAIN_ATTRIB_F_TRIANGLES = 9527;

	/// <summary>Operation invalid, 3D object model already contains lines </summary>
	public const int Jl_ERR_OM3D_CONTAIN_ATTRIB_F_LINES = 9528;

	/// <summary>Operation invalid, 3D object model already contains faces or polygons </summary>
	public const int Jl_ERR_OM3D_CONTAIN_ATTRIB_F_POLYGONS = 9529;

	/// <summary>In a global registration an input object has no neighbors </summary>
	public const int Jl_ERR_OM3D_ISOLATED_OBJECT = 9530;

	/// <summary>All components of points must be set at once </summary>
	public const int Jl_ERR_OM3D_SET_ALL_COORD = 9531;

	/// <summary>All components of normals must be set at once </summary>
	public const int Jl_ERR_OM3D_SET_ALL_NORMALS = 9532;

	/// <summary>Number of values doesn't correspond to number of already existing points </summary>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_COORD = 9533;

	/// <summary>Number of values doesn't correspond to number of already existing normals </summary>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_NORMALS = 9534;

	/// <summary>Number of values doesn't correspond to already existing triangulation </summary>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_TRIANGLES = 9535;

	/// <summary>Number of values doesn't correspond to length of already existing polygons </summary>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_POLYGONS = 9536;

	/// <summary>Number of values doesn't correspond to length of already existing polylines </summary>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_LINES = 9537;

	/// <summary>Number of values doesn't correspond already existing 2D mapping </summary>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_2DMAP = 9538;

	/// <summary>Number of values doesn't correspond to already existing extended attribute </summary>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_EXTENDED = 9539;

	/// <summary>Per-face intensity is used with point attribute </summary>
	public const int Jl_ERR_OM3D_FACE_INTENSITY_WITH_POINTS = 9540;

	/// <summary>Attribute is not (yet) supported </summary>
	public const int Jl_ERR_OM3D_ATTRIBUTE_NOT_SUPPORTED = 9541;

	/// <summary>No point within bounding box </summary>
	public const int Jl_ERR_OM3D_NOT_IN_BB = 9542;

	/// <summary>distance_in_front is smaller than the resolution </summary>
	public const int Jl_ERR_DIF_TOO_SMALL = 9543;

	/// <summary>The minimum thickness is smaller than the surface tolerance </summary>
	public const int Jl_ERR_MINTH_TOO_SMALL = 9544;

	/// <summary>Input width or height does not match the number of points in 3D object model </summary>
	public const int Jl_ERR_OM3D_WRONG_DIMENSION = 9545;

	/// <summary>Image width or height must be set </summary>
	public const int Jl_ERR_OM3D_MISSING_DIMENSION = 9546;

	/// <summary>Triangles of the 3D object model are not suitable for this operator </summary>
	public const int Jl_ERR_SF_OM3D_TRIANGLES_NOT_SUITABLE = 9550;

	/// <summary>Too few suitable 3D points in the 3D object model </summary>
	public const int Jl_ERR_SF_OM3D_FEW_POINTS = 9551;

	/// <summary>Not a valid serialized item file </summary>
	public const int Jl_ERR_NO_SERIALIZED_ITEM = 9580;

	/// <summary>Serialized item: premature end of file </summary>
	public const int Jl_ERR_END_OF_FILE = 9581;

	/// <summary>More than one user thread still uses Vision * resources during finalization </summary>
	public const int Jl_ERR_FINI_USR_THREADS = 9700;

	/// <summary>Invalid file format for encrypted items </summary>
	public const int Jl_ERR_NO_ENCRYPTED_ITEM = 9800;

	/// <summary>Wrong password </summary>
	public const int Jl_ERR_WRONG_PASSWORD = 9801;

	/// <summary>Encryption failed </summary>
	public const int Jl_ERR_ENCRYPT_FAILED = 9802;

	/// <summary>Decryption failed </summary>
	public const int Jl_ERR_DECRYPT_FAILED = 9803;

	/// <summary>User defined error codes must be larger than this value </summary>
	public const int Jl_ERR_START_EXT = 10000;

	/// <summary>No license found </summary>
	public const int Jl_ERR_NO_LICENSE = 2003;

	/// <summary>No modules in license (no VENDOR_STRING) </summary>
	public const int Jl_ERR_NO_MODULES = 2005;

	/// <summary>No license for this operator </summary>
	public const int Jl_ERR_NO_LIC_OPER = 2006;

	/// <summary>!JlERRORDEF_H </summary>
	public const int Jl_ERR_LAST_LIC_ERROR = 2384;
}
