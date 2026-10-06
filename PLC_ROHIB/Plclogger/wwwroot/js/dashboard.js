/**
 * PANASONIC PRODUCTION PLAN & PLC DISPATCHER (LIGHT THEME - WARNA PUTIH)
 * 
 * Fitur Utama:
 * 1. Filter Tanggal Rencana (Hanya menampilkan model dari tanggal yang dipilih).
 * 2. Ceklist Shift Kerja Operator (Shift 1, Shift 2, Shift 3, Non-Shift).
 * 3. Dropdown Cerdas: Menampilkan status model (apakah sudah dipakai di baris/shift lain).
 * 4. Model yang sama boleh dipilih di beberapa baris (tanpa peringatan duplikat).
 * 5. Auto-Fill Quantity (Prod. Plan) & SUT otomatis saat Model dipilih.
 * 6. Urutan Antrian: kotak urutan ditahan & digeser (drag & drop) untuk mengatur urutan produksi, plus tanggal plan.
 * 7. Live Preview Replika Layar HMI GOT (B-5 9 Baris + DEFECT R1014..R1174).
 * 8. Konfirmasi & Dispatch ke PLC Mitsubishi (192.168.1.30:5010 MC Protocol).
 */

// Mapping 120 Baris Register PLC (Halaman HMI GOT: Plan Production 1 - 15)
const PLC_REGISTER_MAP = [
  // --- PAGE 1: PLAN PRODUCTION 1 (ROW 1 - 8) ---
  { rowNo: 1,  page: 1, mAddr: 'R1000', pAddr: 'R1010', sAddr: 'R1011', actAddr: 'R1012', defAddr: 'R1014', mId: 10003, pId: 10021, sId: 10029, actId: 10047, defId: 10055, noId: 10065, noAddr: 'R999' },
  { rowNo: 2,  page: 1, mAddr: 'R1020', pAddr: 'R1030', sAddr: 'R1031', actAddr: 'R1032', defAddr: 'R1034', mId: 10005, pId: 10022, sId: 10031, actId: 10048, defId: 10056, noId: 10066, noAddr: 'R999' },
  { rowNo: 3,  page: 1, mAddr: 'R1040', pAddr: 'R1050', sAddr: 'R1051', actAddr: 'R1052', defAddr: 'R1054', mId: 10006, pId: 10023, sId: 10033, actId: 10049, defId: 10057, noId: 10067, noAddr: 'R999' },
  { rowNo: 4,  page: 1, mAddr: 'R1060', pAddr: 'R1070', sAddr: 'R1071', actAddr: 'R1072', defAddr: 'R1074', mId: 10007, pId: 10024, sId: 10035, actId: 10050, defId: 10058, noId: 10068, noAddr: 'R999' },
  { rowNo: 5,  page: 1, mAddr: 'R1080', pAddr: 'R1090', sAddr: 'R1091', actAddr: 'R1092', defAddr: 'R1094', mId: 10008, pId: 10025, sId: 10037, actId: 10051, defId: 10059, noId: 10069, noAddr: 'R999' },
  { rowNo: 6,  page: 1, mAddr: 'R1100', pAddr: 'R1110', sAddr: 'R1111', actAddr: 'R1112', defAddr: 'R1114', mId: 10009, pId: 10026, sId: 10039, actId: 10052, defId: 10060, noId: 10070, noAddr: 'R999' },
  { rowNo: 7,  page: 1, mAddr: 'R1120', pAddr: 'R1130', sAddr: 'R1131', actAddr: 'R1132', defAddr: 'R1134', mId: 10019, pId: 10027, sId: 10041, actId: 10053, defId: 10061, noId: 10071, noAddr: 'R999' },
  { rowNo: 8,  page: 1, mAddr: 'R1140', pAddr: 'R1150', sAddr: 'R1151', actAddr: 'R1152', defAddr: 'R1154', mId: 10020, pId: 10028, sId: 10043, actId: 10054, defId: 10062, noId: 10072, noAddr: 'R999' },

  // --- PAGE 2: PLAN PRODUCTION 2 (ROW 9 - 16) ---
  { rowNo: 9,  page: 2, mAddr: 'R1160', pAddr: 'R1170', sAddr: 'R1171', actAddr: 'R1172', defAddr: 'R1174', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 10, page: 2, mAddr: 'R1180', pAddr: 'R1190', sAddr: 'R1191', actAddr: 'R1192', defAddr: 'R1194', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 11, page: 2, mAddr: 'R1200', pAddr: 'R1210', sAddr: 'R1211', actAddr: 'R1212', defAddr: 'R1214', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 12, page: 2, mAddr: 'R1220', pAddr: 'R1230', sAddr: 'R1231', actAddr: 'R1232', defAddr: 'R1234', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 13, page: 2, mAddr: 'R1240', pAddr: 'R1250', sAddr: 'R1251', actAddr: 'R1252', defAddr: 'R1254', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 14, page: 2, mAddr: 'R1260', pAddr: 'R1270', sAddr: 'R1271', actAddr: 'R1272', defAddr: 'R1274', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 15, page: 2, mAddr: 'R1280', pAddr: 'R1290', sAddr: 'R1291', actAddr: 'R1292', defAddr: 'R1294', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 16, page: 2, mAddr: 'R1300', pAddr: 'R1310', sAddr: 'R1311', actAddr: 'R1312', defAddr: 'R1314', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 3: PLAN PRODUCTION 3 (ROW 17 - 24) ---
  { rowNo: 17, page: 3, mAddr: 'R1320', pAddr: 'R1330', sAddr: 'R1331', actAddr: 'R1332', defAddr: 'R1334', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 18, page: 3, mAddr: 'R1340', pAddr: 'R1350', sAddr: 'R1351', actAddr: 'R1352', defAddr: 'R1354', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 19, page: 3, mAddr: 'R1360', pAddr: 'R1370', sAddr: 'R1371', actAddr: 'R1372', defAddr: 'R1374', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 20, page: 3, mAddr: 'R1380', pAddr: 'R1390', sAddr: 'R1391', actAddr: 'R1392', defAddr: 'R1394', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 21, page: 3, mAddr: 'R1400', pAddr: 'R1410', sAddr: 'R1411', actAddr: 'R1412', defAddr: 'R1414', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 22, page: 3, mAddr: 'R1420', pAddr: 'R1430', sAddr: 'R1431', actAddr: 'R1432', defAddr: 'R1434', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 23, page: 3, mAddr: 'R1440', pAddr: 'R1450', sAddr: 'R1451', actAddr: 'R1452', defAddr: 'R1454', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 24, page: 3, mAddr: 'R1460', pAddr: 'R1470', sAddr: 'R1471', actAddr: 'R1472', defAddr: 'R1474', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 4: PLAN PRODUCTION 4 (ROW 25 - 32) ---
  { rowNo: 25, page: 4, mAddr: 'R1480', pAddr: 'R1490', sAddr: 'R1491', actAddr: 'R1492', defAddr: 'R1494', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 26, page: 4, mAddr: 'R1500', pAddr: 'R1510', sAddr: 'R1511', actAddr: 'R1512', defAddr: 'R1514', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 27, page: 4, mAddr: 'R1520', pAddr: 'R1530', sAddr: 'R1531', actAddr: 'R1532', defAddr: 'R1534', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 28, page: 4, mAddr: 'R1540', pAddr: 'R1550', sAddr: 'R1551', actAddr: 'R1552', defAddr: 'R1554', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 29, page: 4, mAddr: 'R1560', pAddr: 'R1570', sAddr: 'R1571', actAddr: 'R1572', defAddr: 'R1574', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 30, page: 4, mAddr: 'R1580', pAddr: 'R1590', sAddr: 'R1591', actAddr: 'R1592', defAddr: 'R1594', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 31, page: 4, mAddr: 'R1600', pAddr: 'R1610', sAddr: 'R1611', actAddr: 'R1612', defAddr: 'R1614', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 32, page: 4, mAddr: 'R1620', pAddr: 'R1630', sAddr: 'R1631', actAddr: 'R1632', defAddr: 'R1634', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 5: PLAN PRODUCTION 5 (ROW 33 - 40) ---
  { rowNo: 33, page: 5, mAddr: 'R1640', pAddr: 'R1650', sAddr: 'R1651', actAddr: 'R1652', defAddr: 'R1654', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 34, page: 5, mAddr: 'R1660', pAddr: 'R1670', sAddr: 'R1671', actAddr: 'R1672', defAddr: 'R1674', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 35, page: 5, mAddr: 'R1680', pAddr: 'R1690', sAddr: 'R1691', actAddr: 'R1692', defAddr: 'R1694', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 36, page: 5, mAddr: 'R1700', pAddr: 'R1710', sAddr: 'R1711', actAddr: 'R1712', defAddr: 'R1714', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 37, page: 5, mAddr: 'R1720', pAddr: 'R1730', sAddr: 'R1731', actAddr: 'R1732', defAddr: 'R1734', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 38, page: 5, mAddr: 'R1740', pAddr: 'R1750', sAddr: 'R1751', actAddr: 'R1752', defAddr: 'R1754', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 39, page: 5, mAddr: 'R1760', pAddr: 'R1770', sAddr: 'R1771', actAddr: 'R1772', defAddr: 'R1774', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 40, page: 5, mAddr: 'R1780', pAddr: 'R1790', sAddr: 'R1791', actAddr: 'R1792', defAddr: 'R1794', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 6: PLAN PRODUCTION 6 (ROW 41 - 48) ---
  { rowNo: 41, page: 6, mAddr: 'R1800', pAddr: 'R1810', sAddr: 'R1811', actAddr: 'R1812', defAddr: 'R1814', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 42, page: 6, mAddr: 'R1820', pAddr: 'R1830', sAddr: 'R1831', actAddr: 'R1832', defAddr: 'R1834', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 43, page: 6, mAddr: 'R1840', pAddr: 'R1850', sAddr: 'R1851', actAddr: 'R1852', defAddr: 'R1854', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 44, page: 6, mAddr: 'R1860', pAddr: 'R1870', sAddr: 'R1871', actAddr: 'R1872', defAddr: 'R1874', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 45, page: 6, mAddr: 'R1880', pAddr: 'R1890', sAddr: 'R1891', actAddr: 'R1892', defAddr: 'R1894', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 46, page: 6, mAddr: 'R1900', pAddr: 'R1910', sAddr: 'R1911', actAddr: 'R1912', defAddr: 'R1914', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 47, page: 6, mAddr: 'R1920', pAddr: 'R1930', sAddr: 'R1931', actAddr: 'R1932', defAddr: 'R1934', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 48, page: 6, mAddr: 'R1940', pAddr: 'R1950', sAddr: 'R1951', actAddr: 'R1952', defAddr: 'R1954', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 7: PLAN PRODUCTION 7 (ROW 49 - 56) ---
  { rowNo: 49, page: 7, mAddr: 'R6000', pAddr: 'R6010', sAddr: 'R6011', actAddr: 'R6012', defAddr: 'R6014', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 50, page: 7, mAddr: 'R6020', pAddr: 'R6030', sAddr: 'R6031', actAddr: 'R6032', defAddr: 'R6034', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 51, page: 7, mAddr: 'R6040', pAddr: 'R6050', sAddr: 'R6051', actAddr: 'R6052', defAddr: 'R6054', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 52, page: 7, mAddr: 'R6060', pAddr: 'R6070', sAddr: 'R6071', actAddr: 'R6072', defAddr: 'R6074', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 53, page: 7, mAddr: 'R6080', pAddr: 'R6090', sAddr: 'R6091', actAddr: 'R6092', defAddr: 'R6094', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 54, page: 7, mAddr: 'R6100', pAddr: 'R6110', sAddr: 'R6111', actAddr: 'R6112', defAddr: 'R6114', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 55, page: 7, mAddr: 'R6120', pAddr: 'R6130', sAddr: 'R6131', actAddr: 'R6132', defAddr: 'R6134', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 56, page: 7, mAddr: 'R6140', pAddr: 'R6150', sAddr: 'R6151', actAddr: 'R6152', defAddr: 'R6154', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 8: PLAN PRODUCTION 8 (ROW 57 - 64) ---
  { rowNo: 57, page: 8, mAddr: 'R6180', pAddr: 'R6190', sAddr: 'R6191', actAddr: 'R6192', defAddr: 'R6194', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 58, page: 8, mAddr: 'R6200', pAddr: 'R6210', sAddr: 'R6211', actAddr: 'R6212', defAddr: 'R6214', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 59, page: 8, mAddr: 'R6220', pAddr: 'R6230', sAddr: 'R6231', actAddr: 'R6232', defAddr: 'R6234', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 60, page: 8, mAddr: 'R6240', pAddr: 'R6250', sAddr: 'R6251', actAddr: 'R6252', defAddr: 'R6254', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 61, page: 8, mAddr: 'R6260', pAddr: 'R6270', sAddr: 'R6271', actAddr: 'R6272', defAddr: 'R6274', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 62, page: 8, mAddr: 'R6280', pAddr: 'R6290', sAddr: 'R6291', actAddr: 'R6292', defAddr: 'R6294', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 63, page: 8, mAddr: 'R6300', pAddr: 'R6310', sAddr: 'R6311', actAddr: 'R6312', defAddr: 'R6314', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 64, page: 8, mAddr: 'R6320', pAddr: 'R6330', sAddr: 'R6331', actAddr: 'R6332', defAddr: 'R6334', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 9: PLAN PRODUCTION 9 (ROW 65 - 72) ---
  { rowNo: 65, page: 9, mAddr: 'R6340', pAddr: 'R6350', sAddr: 'R6351', actAddr: 'R6352', defAddr: 'R6354', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 66, page: 9, mAddr: 'R6360', pAddr: 'R6370', sAddr: 'R6371', actAddr: 'R6372', defAddr: 'R6374', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 67, page: 9, mAddr: 'R6380', pAddr: 'R6390', sAddr: 'R6391', actAddr: 'R6392', defAddr: 'R6394', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 68, page: 9, mAddr: 'R6400', pAddr: 'R6410', sAddr: 'R6411', actAddr: 'R6412', defAddr: 'R6414', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 69, page: 9, mAddr: 'R6420', pAddr: 'R6430', sAddr: 'R6431', actAddr: 'R6432', defAddr: 'R6434', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 70, page: 9, mAddr: 'R6440', pAddr: 'R6450', sAddr: 'R6451', actAddr: 'R6452', defAddr: 'R6454', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 71, page: 9, mAddr: 'R6460', pAddr: 'R6470', sAddr: 'R6471', actAddr: 'R6472', defAddr: 'R6474', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 72, page: 9, mAddr: 'R6480', pAddr: 'R6490', sAddr: 'R6491', actAddr: 'R6492', defAddr: 'R6494', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 10: PLAN PRODUCTION 10 (ROW 73 - 80) ---
  { rowNo: 73, page: 10, mAddr: 'R6500', pAddr: 'R6510', sAddr: 'R6511', actAddr: 'R6512', defAddr: 'R6514', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 74, page: 10, mAddr: 'R6520', pAddr: 'R6530', sAddr: 'R6531', actAddr: 'R6532', defAddr: 'R6534', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 75, page: 10, mAddr: 'R6540', pAddr: 'R6550', sAddr: 'R6551', actAddr: 'R6552', defAddr: 'R6554', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 76, page: 10, mAddr: 'R6560', pAddr: 'R6570', sAddr: 'R6571', actAddr: 'R6572', defAddr: 'R6574', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 77, page: 10, mAddr: 'R6580', pAddr: 'R6590', sAddr: 'R6591', actAddr: 'R6592', defAddr: 'R6594', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 78, page: 10, mAddr: 'R6600', pAddr: 'R6610', sAddr: 'R6611', actAddr: 'R6612', defAddr: 'R6614', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 79, page: 10, mAddr: 'R6620', pAddr: 'R6630', sAddr: 'R6631', actAddr: 'R6632', defAddr: 'R6634', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 80, page: 10, mAddr: 'R6640', pAddr: 'R6650', sAddr: 'R6651', actAddr: 'R6652', defAddr: 'R6654', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 11: PLAN PRODUCTION 11 (ROW 81 - 88) ---
  { rowNo: 81, page: 11, mAddr: 'R6660', pAddr: 'R6670', sAddr: 'R6671', actAddr: 'R6672', defAddr: 'R6674', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 82, page: 11, mAddr: 'R6680', pAddr: 'R6690', sAddr: 'R6691', actAddr: 'R6692', defAddr: 'R6694', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 83, page: 11, mAddr: 'R6700', pAddr: 'R6710', sAddr: 'R6711', actAddr: 'R6712', defAddr: 'R6714', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 84, page: 11, mAddr: 'R6720', pAddr: 'R6730', sAddr: 'R6731', actAddr: 'R6732', defAddr: 'R6734', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 85, page: 11, mAddr: 'R6740', pAddr: 'R6750', sAddr: 'R6751', actAddr: 'R6752', defAddr: 'R6754', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 86, page: 11, mAddr: 'R6760', pAddr: 'R6770', sAddr: 'R6771', actAddr: 'R6772', defAddr: 'R6774', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 87, page: 11, mAddr: 'R6780', pAddr: 'R6790', sAddr: 'R6791', actAddr: 'R6792', defAddr: 'R6794', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 88, page: 11, mAddr: 'R6800', pAddr: 'R6810', sAddr: 'R6811', actAddr: 'R6812', defAddr: 'R6814', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 12: PLAN PRODUCTION 12 (ROW 89 - 96) ---
  { rowNo: 89, page: 12, mAddr: 'R6820', pAddr: 'R6830', sAddr: 'R6831', actAddr: 'R6832', defAddr: 'R6834', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 90, page: 12, mAddr: 'R6840', pAddr: 'R6850', sAddr: 'R6851', actAddr: 'R6852', defAddr: 'R6854', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 91, page: 12, mAddr: 'R6860', pAddr: 'R6870', sAddr: 'R6871', actAddr: 'R6872', defAddr: 'R6874', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 92, page: 12, mAddr: 'R6880', pAddr: 'R6890', sAddr: 'R6891', actAddr: 'R6892', defAddr: 'R6894', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 93, page: 12, mAddr: 'R6900', pAddr: 'R6910', sAddr: 'R6911', actAddr: 'R6912', defAddr: 'R6914', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 94, page: 12, mAddr: 'R6920', pAddr: 'R6930', sAddr: 'R6931', actAddr: 'R6932', defAddr: 'R6934', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 95, page: 12, mAddr: 'R6940', pAddr: 'R6950', sAddr: 'R6951', actAddr: 'R6952', defAddr: 'R6954', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 96, page: 12, mAddr: 'R6960', pAddr: 'R6970', sAddr: 'R6971', actAddr: 'R6972', defAddr: 'R6974', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 13: PLAN PRODUCTION 13 (ROW 97 - 104) ---
  { rowNo: 97, page: 13, mAddr: 'R6980', pAddr: 'R6990', sAddr: 'R6991', actAddr: 'R6992', defAddr: 'R6994', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 98, page: 13, mAddr: 'R7000', pAddr: 'R7010', sAddr: 'R7011', actAddr: 'R7012', defAddr: 'R7014', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 99, page: 13, mAddr: 'R7020', pAddr: 'R7030', sAddr: 'R7031', actAddr: 'R7032', defAddr: 'R7034', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 100, page: 13, mAddr: 'R7040', pAddr: 'R7050', sAddr: 'R7051', actAddr: 'R7052', defAddr: 'R7054', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 101, page: 13, mAddr: 'R7060', pAddr: 'R7070', sAddr: 'R7071', actAddr: 'R7072', defAddr: 'R7074', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 102, page: 13, mAddr: 'R7080', pAddr: 'R7090', sAddr: 'R7091', actAddr: 'R7092', defAddr: 'R7094', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 103, page: 13, mAddr: 'R7100', pAddr: 'R7110', sAddr: 'R7111', actAddr: 'R7112', defAddr: 'R7114', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 104, page: 13, mAddr: 'R7120', pAddr: 'R7130', sAddr: 'R7131', actAddr: 'R7132', defAddr: 'R7134', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 14: PLAN PRODUCTION 14 (ROW 105 - 112) ---
  { rowNo: 105, page: 14, mAddr: 'R7140', pAddr: 'R7150', sAddr: 'R7151', actAddr: 'R7152', defAddr: 'R7154', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 106, page: 14, mAddr: 'R7160', pAddr: 'R7170', sAddr: 'R7171', actAddr: 'R7172', defAddr: 'R7174', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 107, page: 14, mAddr: 'R7180', pAddr: 'R7190', sAddr: 'R7191', actAddr: 'R7192', defAddr: 'R7194', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 108, page: 14, mAddr: 'R7200', pAddr: 'R7210', sAddr: 'R7211', actAddr: 'R7212', defAddr: 'R7214', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 109, page: 14, mAddr: 'R7220', pAddr: 'R7230', sAddr: 'R7231', actAddr: 'R7232', defAddr: 'R7234', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 110, page: 14, mAddr: 'R7240', pAddr: 'R7250', sAddr: 'R7251', actAddr: 'R7252', defAddr: 'R7254', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 111, page: 14, mAddr: 'R7260', pAddr: 'R7270', sAddr: 'R7271', actAddr: 'R7272', defAddr: 'R7274', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 112, page: 14, mAddr: 'R7280', pAddr: 'R7290', sAddr: 'R7291', actAddr: 'R7292', defAddr: 'R7294', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' },

  // --- PAGE 15: PLAN PRODUCTION 15 (ROW 113 - 120) ---
  { rowNo: 113, page: 15, mAddr: 'R7300', pAddr: 'R7310', sAddr: 'R7311', actAddr: 'R7312', defAddr: 'R7314', mId: 10014, pId: 10031, sId: 10039, actId: 10057, defId: 10065, noId: 10075, noAddr: 'R999' },
  { rowNo: 114, page: 15, mAddr: 'R7320', pAddr: 'R7330', sAddr: 'R7331', actAddr: 'R7332', defAddr: 'R7334', mId: 10015, pId: 10032, sId: 10041, actId: 10058, defId: 10066, noId: 10076, noAddr: 'R999' },
  { rowNo: 115, page: 15, mAddr: 'R7340', pAddr: 'R7350', sAddr: 'R7351', actAddr: 'R7352', defAddr: 'R7354', mId: 10016, pId: 10033, sId: 10043, actId: 10059, defId: 10067, noId: 10077, noAddr: 'R999' },
  { rowNo: 116, page: 15, mAddr: 'R7360', pAddr: 'R7370', sAddr: 'R7371', actAddr: 'R7372', defAddr: 'R7374', mId: 10017, pId: 10034, sId: 10045, actId: 10060, defId: 10068, noId: 10078, noAddr: 'R999' },
  { rowNo: 117, page: 15, mAddr: 'R7380', pAddr: 'R7390', sAddr: 'R7391', actAddr: 'R7392', defAddr: 'R7394', mId: 10018, pId: 10035, sId: 10047, actId: 10061, defId: 10069, noId: 10079, noAddr: 'R999' },
  { rowNo: 118, page: 15, mAddr: 'R7400', pAddr: 'R7410', sAddr: 'R7411', actAddr: 'R7412', defAddr: 'R7414', mId: 10019, pId: 10036, sId: 10049, actId: 10062, defId: 10070, noId: 10080, noAddr: 'R999' },
  { rowNo: 119, page: 15, mAddr: 'R7420', pAddr: 'R7430', sAddr: 'R7431', actAddr: 'R7432', defAddr: 'R7434', mId: 10029, pId: 10037, sId: 10051, actId: 10063, defId: 10071, noId: 10081, noAddr: 'R999' },
  { rowNo: 120, page: 15, mAddr: 'R7440', pAddr: 'R7450', sAddr: 'R7451', actAddr: 'R7452', defAddr: 'R7454', mId: 10030, pId: 10038, sId: 10053, actId: 10064, defId: 10072, noId: 10082, noAddr: 'R999' }
];

// Jumlah baris & halaman HMI GOT yang aktif (8 baris per halaman)
const ROWS_PER_HMI_PAGE = 8;
const TOTAL_PLC_ROWS = PLC_REGISTER_MAP.length;
// Nomor halaman HMI yang ada di aplikasi (urut sesuai PLC_REGISTER_MAP, boleh loncat, mis. 1-6 lalu 8)
const HMI_PAGES = [...new Set(PLC_REGISTER_MAP.map(r => r.page))];
const LAST_HMI_PAGE = HMI_PAGES[HMI_PAGES.length - 1];

// Label Shift (diperbarui sesuai jam aktual Panasonic)
const SHIFT_LABELS = {
  'NS': 'Non-Shift (NS) [07:00 - 16:00]',
  '1': 'Shift 1 (Pagi) [07:00 - 15:45]',
  '2': 'Shift 2 (Sore) [15:45 - 23:15]',
  '3': 'Shift 3 (Malam) [23:15 - 07:00]'
};

// Global State
let rawSapPlanItems = [];
let currentProductionPlan = null;
let currentShift = 'ALL';
let currentMachine = 'MCH1-01'; // Default mesin CU
let currentUnitType = 'CU';      // Default tipe produk CU murni
// Tanggal hari ini (jam lokal PC, format YYYY-MM-DD). Pilihan tanggal sudah dihapus:
// saran model dari rencana SapPlan selalu memakai tanggal hari ini.
function getTodayLocalDate() {
  const d = new Date();
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
let selectedDate = getTodayLocalDate();

// Data 48 Baris yang Aktif (8 baris per halaman: Plan Production 1 - 6)
let current9Rows = [];
let currentHmiPage = 1; // salah satu nomor di HMI_PAGES

document.addEventListener('DOMContentLoaded', () => {
  setupShiftChecklist();
  setupMachineFilters();
  setupActionButtons();
  loadMachineList();
  buildEmptyRows();          // Editor kosong dulu, lalu diisi dari database (tabel PlcRohibEditorRow)
  startEditorDraftAutosave(); // Muat isi editor tersimpan + simpan otomatis setiap ada perubahan
  loadMasterSutMap();        // SUT per model dari Master Data Produk
  loadPlanData(selectedDate); // Rencana SapPlan hanya sebagai saran model

  // Polling: baca R100/R101 dari PLC + update overtime setiap 15 detik
  startPlcStatusPolling();

  // Polling: cek akhir shift setiap 30 detik
  startShiftEndPolling();

  // Polling: auto-refresh nilai ACTUAL & DEFECT dari PLC secara real-time setiap 3 detik
  startPlcActualPolling();

  // Polling: kolom ACTUAL editor dari output Inventory AC OEE per model setiap 60 detik
  startInventoryActualPolling();

  // Setup popup modal
  setupShiftEndModal();

  // Test popup via URL param ?testPopup=1
  if (new URLSearchParams(window.location.search).get('testPopup') === '1') {
    setTimeout(() => openShiftEndModal({ currentShift: 'Shift 1 (Pagi)', minutesLeft: 3, secondsLeft: 180, nextShift: 'Shift 2 (15:45)' }), 500);
  }
});

// =========================================================================
// CUSTOM APP MODAL & NOTIFICATION POPUP (USER-FRIENDLY & CLEAN)
// =========================================================================
function showAppModal(optsOrType = 'warning', titleArg = '', messageArg = '') {
  let opts = {};
  if (typeof optsOrType === 'object' && optsOrType !== null) {
    opts = optsOrType;
  } else {
    opts = {
      type: optsOrType,
      title: titleArg || (optsOrType === 'error' ? 'Terjadi Kesalahan' : optsOrType === 'success' ? 'Berhasil' : 'Pemberitahuan'),
      message: messageArg,
      badge: optsOrType === 'error' ? 'Error' : optsOrType === 'success' ? 'Sukses' : optsOrType === 'warning' ? 'Peringatan' : 'Info'
    };
  }

  const {
    type = 'warning', // 'warning', 'error', 'success', 'info'
    title = 'Pemberitahuan',
    badge = 'Info',
    message = '',
    tip = '',
    buttonText = 'Saya Mengerti',
    onClose = null
  } = opts;
  const backdrop = document.getElementById('appModalBackdrop');
  const dialog = document.getElementById('appModalDialog');
  const iconEl = document.getElementById('appModalIcon');
  const titleEl = document.getElementById('appModalTitle');
  const badgeEl = document.getElementById('appModalBadge');
  const msgEl = document.getElementById('appModalMainMessage');
  const tipBox = document.getElementById('appModalTipBox');
  const tipEl = document.getElementById('appModalTipText');
  const closeBtn = document.getElementById('appModalCloseBtn');
  const primaryBtn = document.getElementById('appModalPrimaryBtn');

  if (!backdrop || !dialog) return;

  // Set type variant class
  dialog.className = `app-modal-dialog type-${type}`;

  // Set icon
  const icons = {
    warning: '⚠️',
    error: '❌',
    success: '✅',
    info: 'ℹ️'
  };
  if (iconEl) iconEl.innerText = icons[type] || 'ℹ️';

  // Set title & badge
  if (titleEl) titleEl.innerText = title;
  if (badgeEl) badgeEl.innerText = badge;

  // Set main message (HTML enabled for bold text)
  if (msgEl) msgEl.innerHTML = message;

  // Set tip box
  if (tip) {
    if (tipBox) tipBox.style.display = 'flex';
    if (tipEl) tipEl.innerHTML = tip;
  } else {
    if (tipBox) tipBox.style.display = 'none';
  }

  // Set primary button text
  if (primaryBtn) {
    primaryBtn.innerText = buttonText;
  }

  // Close handler
  const closeModal = () => {
    backdrop.classList.remove('show');
    if (typeof onClose === 'function') onClose();
    document.removeEventListener('keydown', onKeyDown);
  };

  const onKeyDown = (e) => {
    if (e.key === 'Escape' || e.key === 'Enter') {
      closeModal();
    }
  };

  if (closeBtn) closeBtn.onclick = closeModal;
  if (primaryBtn) primaryBtn.onclick = closeModal;
  backdrop.onclick = (e) => {
    if (e.target === backdrop) closeModal();
  };
  document.addEventListener('keydown', onKeyDown);

  // Show modal
  backdrop.classList.add('show');
}


// Helper: Menghitung Quantity Prod. Plan dari Database
function getProductPlanQuantity(item) {
  if (!item) return 0;
  if (typeof item.totalPlan === 'number' && item.totalPlan > 0) return item.totalPlan;
  if (typeof item.sapPlanNormal === 'number' && item.sapPlanNormal > 0) return item.sapPlanNormal;
  if (typeof item.sapPlanOvertime === 'number' && item.sapPlanOvertime > 0) return item.sapPlanOvertime;
  return 0;
}

// 2. Setup Ceklist Shift
function setupShiftChecklist() {
  const cards = document.querySelectorAll('.shift-check-item');
  cards.forEach(card => {
    card.addEventListener('click', () => {
      const shiftVal = card.getAttribute('data-shift');
      selectOperatorShift(shiftVal);
    });
  });
}

function selectOperatorShift(shiftCode) {
  currentShift = shiftCode;

  // Update Tampilan Ceklist Cards
  document.querySelectorAll('.shift-check-item').forEach(card => {
    const isSelected = card.getAttribute('data-shift') === shiftCode;
    card.classList.toggle('selected', isSelected);
    const radio = card.querySelector('input[type="radio"]');
    if (radio) radio.checked = isSelected;
  });

  // Update Badge Shift
  const badge = document.getElementById('activeShiftIndicatorBadge');
  if (badge) {
    const label = SHIFT_LABELS[shiftCode] || `Shift ${shiftCode}`;
    badge.innerHTML = `Shift Aktif Terpilih: <strong>${label}</strong>`;
  }

  addLog(`[OPERATOR CEKLIST] Shift "${SHIFT_LABELS[shiftCode]}" aktif. Saran model disesuaikan dengan rencana shift ini.`, 'info');

  updateAllDatalists();
}

// 3. Setup Filter Mesin & Tipe Unit (Khusus Mesin CU: MCH1-01)
function setupMachineFilters() {
  currentUnitType = 'CU';
  currentMachine = 'MCH1-01';

  const unitFilter = document.getElementById('unitTypeFilter');
  if (unitFilter) {
    unitFilter.value = 'CU';
    unitFilter.addEventListener('change', () => {
      currentUnitType = 'CU';
      updateAllDatalists();
    });
  }

  const machineSelect = document.getElementById('machineSelectFilter');
  if (machineSelect) {
    machineSelect.value = 'MCH1-01';
    machineSelect.addEventListener('change', () => {
      currentMachine = 'MCH1-01';
      updateAllDatalists();
    });
  }
}

// 4. Setup Tombol Aksi
function setupActionButtons() {
  const btnReset = document.getElementById('btnResetToDb');
  if (btnReset) {
    btnReset.addEventListener('click', () => populate9RowsFromDatabase());
  }

  // Pilihan bulan list rencana: bulan ini + 3 bulan ke depan (sama dengan horizon worker SAP Plan)
  const monthSel = document.getElementById('psiMonthSelect');
  if (monthSel) {
    const now = new Date();
    for (let i = 0; i < 4; i++) {
      const d = new Date(now.getFullYear(), now.getMonth() + i, 1);
      const value = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
      const label = d.toLocaleDateString('id-ID', { month: 'long', year: 'numeric' });
      monthSel.add(new Option(label, value));
    }
  }

  const btnConfirmAll = document.getElementById('btnConfirmDispatchAll');
  if (btnConfirmAll) {
    btnConfirmAll.addEventListener('click', () => dispatchToPlc(false));
  }

  const btnConfirmP1 = document.getElementById('btnConfirmDispatchP1');
  if (btnConfirmP1) {
    btnConfirmP1.addEventListener('click', () => dispatchToPlc(true));
  }

  const btnReadPlc = document.getElementById('btnReadPlcNow');
  if (btnReadPlc) {
    btnReadPlc.addEventListener('click', () => readPlcRegisters(false));
  }
}

// Muat Data Rencana Produksi Sesuai Tanggal yang Dipilih (KHUSUS PRODUK CU)
// Rencana SapPlan HANYA dipakai sebagai saran model; isi editor tidak ditimpa.
async function loadPlanData(date) {
  try {
    addLog(`[DATABASE] Memuat rencana produksi CU untuk tanggal ${date}...`, 'info');
    const res = await fetch(`api/plan?date=${date}`);
    if (!res.ok) throw new Error('Gagal mengambil data dari server');

    const data = await res.json();
    currentProductionPlan = data.productionPlan;

    // KHUSUS MESIN CU: Hanya ambil dan simpan produk CU murni (Abaikan KIOS dan CS)
    rawSapPlanItems = (data.sapPlan || []).filter(x => {
      const p = (x.productName || '').trim().toUpperCase();
      return p.startsWith('CU');
    });

    // Tampilkan PlanId
    const planIdVal = document.getElementById('currentPlanIdDisplay');
    if (planIdVal) {
      planIdVal.innerText = currentProductionPlan ? `(PlanId: ${currentProductionPlan.id})` : '(PlanId: -)';
    }

    // Status Koneksi DB
    const dbStatusEl = document.getElementById('dbStatusText');
    const dbDot = document.getElementById('dbStatusDot');
    if (dbStatusEl) dbStatusEl.innerText = data.isDbLive ? 'DB: 10.83.33.103 Live' : 'DB: Offline';
    if (dbDot) dbDot.className = data.isDbLive ? 'status-dot dot-green' : 'status-dot dot-red';

    if (!data.isDbLive) {
      addLog(`[DATABASE] Database tidak dapat diakses (${data.dbStatus}). Saran model dari rencana tidak tersedia.`, 'error');
    } else if (!currentProductionPlan) {
      addLog(`[DATABASE] Tidak ada rencana produksi (ProductionPlan) untuk tanggal ${date}.`, 'info');
    } else {
      addLog(`[DATABASE] ${rawSapPlanItems.length} produk CU dari rencana tanggal ${date} tersedia sebagai saran model.`, 'success');
    }

    updateAllDatalists();
  } catch (err) {
    addLog(`[ERROR] Gagal memuat data tanggal ${date}: ${err.message}`, 'error');
  }
}

// SUT per model dari Master Data Produk (tabel MasterProduct). Key = nama model huruf besar.
let masterSutMap = {};
let masterModelList = []; // [{ model, sut }] untuk saran model

async function loadMasterSutMap() {
  try {
    const res = await fetch('api/masterproduct');
    if (!res.ok) throw await masterApiError(res);
    const data = await res.json();
    masterSutMap = {};
    masterModelList = [];
    data.forEach(x => {
      const name = (x.model || '').trim();
      if (!name) return;
      masterSutMap[name.toUpperCase()] = Number(x.sut) || 0;
      masterModelList.push({ model: name, sut: Number(x.sut) || 0 });
    });
    updateAllDatalists();
  } catch (err) {
    addLog(`[MASTER DATA] Gagal memuat SUT dari Master Data Produk: ${err.message}`, 'error');
  }
}

function getMasterSut(modelName) {
  return masterSutMap[(modelName || '').trim().toUpperCase()] || 0;
}

// Rencana SapPlan tanggal terpilih yang sesuai shift & line CU MCH1-01
function getFilteredPlanItems() {
  let filtered = rawSapPlanItems.filter(x => (x.productName || '').trim().toUpperCase().startsWith('CU'));
  if (currentShift !== 'ALL') {
    filtered = filtered.filter(x => (x.shift || '').toUpperCase() === currentShift.toUpperCase());
  }
  return filtered.filter(x => !x.machineCode || x.machineCode === 'MCH1-01');
}

const plcModelKey = (name) => (name || '').trim().toUpperCase();

// Satu baris editor. actual = register ACTUAL PLC (hanya untuk preview), dipertahankan dari baris sebelumnya.
// invActual & defect = ACTUAL (Inventory) & DEFECT milik isi baris ini, disimpan di database bersama isi editor.
// planDate = tanggal plan dari list rencana (yyyy-MM-dd), hanya ditampilkan di kolom Urutan Antrian / Tanggal.
function makeEditorRow(i, modelName, prodPlan, sut, planDate = '', savedActual = 0, savedDefect = 0) {
  const reg = PLC_REGISTER_MAP[i] || {
    rowNo: i + 1,
    page: Math.floor(i / ROWS_PER_HMI_PAGE) + 1,
    mAddr: `R${1000 + i * 20}`,
    pAddr: `R${1010 + i * 20}`,
    sAddr: `R${1011 + i * 20}`,
    actAddr: `R${1012 + i * 20}`,
    defAddr: `R${1014 + i * 20}`,
    mId: '-', pId: '-', sId: '-', actId: '-', defId: '-', noId: '-',
    noAddr: 'R999'
  };
  const prev = current9Rows[i];

  return {
    rowNo: reg.rowNo || (i + 1),
    page: reg.page || (Math.floor(i / ROWS_PER_HMI_PAGE) + 1),
    modelAddr: reg.mAddr,
    planAddr: reg.pAddr,
    sutAddr: reg.sAddr,
    actAddr: reg.actAddr,
    defAddr: reg.defAddr,
    mId: reg.mId,
    pId: reg.pId,
    sId: reg.sId,
    actId: reg.actId,
    defId: reg.defId,
    noId: reg.noId,
    noAddr: reg.noAddr,
    modelName: modelName,
    prodPlan: prodPlan,
    sut: sut,
    planDate: planDate || '',
    actual: prev ? prev.actual : 0,
    invActual: savedActual || 0,
    defect: savedDefect || 0
  };
}

// Editor kosong (120 baris) — dipakai saat halaman dibuka
function buildEmptyRows() {
  const rows = [];
  for (let i = 0; i < TOTAL_PLC_ROWS; i++) rows.push(makeEditorRow(i, '', 0, 0));
  current9Rows = rows;
  renderEditableTable();
  renderHmiPreview();
}

// =========================================================
// ISI EDITOR TERSIMPAN DI DATABASE (tabel dbo.PlcRohibEditorRow, per mesin)
// Dimuat saat halaman dibuka; disimpan otomatis ±1 detik setelah isi editor berhenti berubah.
// Penyimpanan baru aktif setelah isi dari database berhasil dimuat, supaya editor kosong
// (mis. saat DB offline) tidak menimpa isi yang sudah tersimpan.
// =========================================================
let _draftLoaded = false;
let _draftSavedSig = null;   // isi terakhir yang sudah ada di database
let _draftPendingSig = null; // isi yang sedang ditunggu stabil 1 detik
let _draftBusy = false;
let _draftRetryAt = 0;       // jeda sebelum mencoba lagi setelah gagal

function editorDraftRows() {
  return current9Rows
    .map(r => ({ rowNo: r.rowNo, modelName: (r.modelName || '').trim(), prodPlan: parseInt(r.prodPlan) || 0, sut: parseInt(r.sut) || 0, planDate: r.planDate || null, actual: parseInt(r.invActual) || 0, defect: parseInt(r.defect) || 0 }))
    .filter(r => r.modelName || r.prodPlan > 0 || r.sut > 0);
}

// Isi yang tersimpan di DB, dalam bentuk & urutan yang sama dengan editorDraftRows() (untuk deteksi perubahan)
function draftSigFromDb(rows) {
  return JSON.stringify((rows || [])
    .map(r => ({ rowNo: r.rowNo, modelName: (r.modelName || '').trim(), prodPlan: r.prodPlan || 0, sut: r.sut || 0, planDate: r.planDate || null, actual: r.actual || 0, defect: r.defect || 0 }))
    .filter(r => r.modelName || r.prodPlan > 0 || r.sut > 0)
    .sort((a, b) => a.rowNo - b.rowNo));
}

function setDraftStatus(state, text) {
  const el = document.getElementById('draftSaveStatus');
  if (!el) return;
  const colors = { ok: '#166534', pending: '#92400e', error: '#b91c1c', wait: '#475569' };
  el.style.color = colors[state] || colors.wait;
  el.innerText = text;
}

async function draftApiError(res) {
  const data = await res.json().catch(() => ({}));
  return new Error(data.message || `HTTP ${res.status}`);
}

async function loadEditorDraft() {
  _draftBusy = true;
  setDraftStatus('wait', '⏳ Memuat isi editor dari DB...');
  try {
    const res = await fetch(`api/editor-draft?machine=${encodeURIComponent(currentMachine)}`);
    if (!res.ok) throw await draftApiError(res);
    const data = await res.json();

    if (editorDraftRows().length > 0) {
      // Operator sudah mengetik saat DB belum terhubung: isi layar dipertahankan dan akan disimpan ke DB.
      _draftSavedSig = draftSigFromDb(data.rows);
      addLog('[DATABASE] DB terhubung kembali. Isi editor di layar dipakai dan disimpan ke database.', 'info');
    } else {
      const byRow = new Map((data.rows || []).map(r => [r.rowNo, r]));
      current9Rows = PLC_REGISTER_MAP.map((reg, i) => {
        const d = byRow.get(reg.rowNo);
        return d ? makeEditorRow(i, d.modelName, d.prodPlan, d.sut, d.planDate, d.actual, d.defect) : makeEditorRow(i, '', 0, 0);
      });
      renderEditableTable();
      _draftSavedSig = draftSigFromDb(data.rows); // ACTUAL yang baru dihitung (beda dari DB) ikut tersimpan
      if (byRow.size > 0) addLog(`[DATABASE] Isi editor dimuat dari database: ${byRow.size} baris (terakhir disimpan ${data.updatedAt}).`, 'info');
    }

    _draftLoaded = true;
    setDraftStatus('ok', data.updatedAt ? `💾 Tersimpan di DB · ${data.updatedAt.slice(11)}` : '💾 Tersimpan di DB');
  } catch (err) {
    _draftRetryAt = Date.now() + 10000;
    setDraftStatus('error', '⚠ DB tidak terhubung: isi editor belum tersimpan');
    addLog(`[DATABASE] Gagal memuat isi editor tersimpan: ${err.message}`, 'error');
  } finally {
    _draftBusy = false;
  }
}

async function saveEditorDraft(sig) {
  _draftBusy = true;
  try {
    const res = await fetch(`api/editor-draft?machine=${encodeURIComponent(currentMachine)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ rows: JSON.parse(sig) })
    });
    if (!res.ok) throw await draftApiError(res);
    const data = await res.json();
    _draftSavedSig = sig;
    setDraftStatus('ok', `💾 Tersimpan di DB · ${data.savedAt}`);
  } catch (err) {
    _draftRetryAt = Date.now() + 10000;
    setDraftStatus('error', '⚠ Gagal simpan ke DB, dicoba lagi...');
    addLog(`[DATABASE] Gagal menyimpan isi editor: ${err.message}`, 'error');
  } finally {
    _draftBusy = false;
  }
}

function startEditorDraftAutosave() {
  loadEditorDraft();

  setInterval(() => {
    if (_draftBusy || Date.now() < _draftRetryAt) return;
    if (!_draftLoaded) { loadEditorDraft(); return; }

    const sig = JSON.stringify(editorDraftRows());
    if (sig === _draftSavedSig) { _draftPendingSig = null; return; }
    if (sig !== _draftPendingSig) {
      // Masih berubah: tunggu 1 detik tanpa perubahan sebelum disimpan
      _draftPendingSig = sig;
      setDraftStatus('pending', '✏️ Menyimpan...');
      return;
    }
    saveEditorDraft(sig);
  }, 1000);

  // Halaman ditutup/di-refresh sebelum sempat tersimpan: kirim sekali lagi di latar belakang
  window.addEventListener('pagehide', () => {
    if (!_draftLoaded) return;
    const sig = JSON.stringify(editorDraftRows());
    if (sig === _draftSavedSig) return;
    fetch(`api/editor-draft?machine=${encodeURIComponent(currentMachine)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ rows: JSON.parse(sig) }),
      keepalive: true
    });
  });
}

// Tombol "Isi dari Rencana Database": isi editor dengan list rencana satu bulan penuh (semua minggu, tabel
// dbo.PsiWeeklyPlan, diisi worker SAP Plan Panamon) sesuai urutan list, Plan = Qty list, SUT dari Master Data Produk.
async function populate9RowsFromDatabase() {
  const monthSel = document.getElementById('psiMonthSelect');
  const month = monthSel && monthSel.value ? monthSel.value : getTodayLocalDate().slice(0, 7);
  const monthLabel = monthSel && monthSel.selectedIndex >= 0 ? monthSel.options[monthSel.selectedIndex].text : month;
  let data;
  try {
    const res = await fetch(`api/psi-weekly?machine=${encodeURIComponent(currentMachine)}&month=${month}`);
    if (!res.ok) throw await draftApiError(res);
    data = await res.json();
  } catch (err) {
    showAppModal({
      type: 'error',
      title: 'Gagal Mengambil List Mingguan',
      message: `List mingguan tidak bisa diambil dari database: <strong>${escapeHtml(err.message)}</strong>`,
      tip: 'Editor tidak diubah. Periksa koneksi database lalu coba lagi.',
      buttonText: 'Tutup'
    });
    return;
  }

  const items = data.rows || [];
  if (items.length === 0) {
    showAppModal({
      type: 'info',
      title: 'Tidak Ada Rencana',
      message: `Belum ada list rencana untuk bulan <strong>${escapeHtml(monthLabel)}</strong>.`,
      tip: 'Editor tidak diubah. List dibuat otomatis oleh worker SAP Plan di Panamon (setiap 3 jam) dari file Daily prod plan.',
      buttonText: 'Mengerti'
    });
    return;
  }

  const filled = editorDraftRows().length;
  const weeks = new Set(items.map(x => x.weekStart)).size;
  if (filled > 0 && !confirm(`Editor berisi ${filled} baris. Ganti dengan list rencana ${monthLabel} (${weeks} minggu, ${items.length} model)?`)) {
    return;
  }

  const totalCount = Math.max(TOTAL_PLC_ROWS, items.length);
  const rows = [];
  for (let i = 0; i < totalCount; i++) {
    const item = items[i] || null;
    rows.push(item
      ? makeEditorRow(i, item.productName, item.qty, getMasterSut(item.productName), item.planDate)
      : makeEditorRow(i, '', 0, 0));
  }
  current9Rows = rows;

  renderEditableTable();
  renderHmiPreview();
  updateAllDatalists();

  addLog(`[DATABASE] Editor diisi ${items.length} model dari list rencana ${monthLabel} (${weeks} minggu, dbo.PsiWeeklyPlan). Belum dikirim ke PLC.`, 'info');
  if (items.length > TOTAL_PLC_ROWS) {
    addLog(`[PERINGATAN] List ${monthLabel} berisi ${items.length} model, lebih dari ${TOTAL_PLC_ROWS} baris HMI. Baris ${TOTAL_PLC_ROWS + 1} dst. tidak ikut dikirim ke PLC.`, 'error');
  }
  const noSut = rows.filter(r => r.modelName && !r.sut).map(r => `Row ${r.rowNo} (${r.modelName})`);
  if (noSut.length > 0) {
    addLog(`[PERINGATAN] SUT belum ada di Master Data Produk untuk: ${noSut.join(', ')}. Isi SUT manual atau tambahkan modelnya di Master Data Produk.`, 'error');
  }
}

// Render Tabel 16 Baris dengan Tombol Urutan (▲ Naik / ▼ Turun / ★ P1) dan Indikator Status Model
function renderEditableTable() {
  const tbody = document.getElementById('editableTableBody');
  if (!tbody) return;
  tbody.innerHTML = '';
  computeInventoryActuals();

  let lastWeekKey = null;
  current9Rows.forEach((row, idx) => {
    // Pembatas minggu (Senin-Minggu, dipotong di batas bulan): muncul setiap kali tanggal plan masuk minggu lain.
    // Baris tanpa tanggal (diisi manual / kosong) tidak memunculkan pembatas.
    const week = planWeekOf(row.planDate);
    if (week && week.key !== lastWeekKey) {
      lastWeekKey = week.key;
      const divTr = document.createElement('tr');
      divTr.className = 'week-divider-row';
      divTr.innerHTML = `
        <td colspan="7" style="background:#f0fdf4; border-top:2px dashed #16a34a; border-bottom:2px dashed #16a34a; padding:7px 12px; text-align:center; font-weight:800; color:#166534; font-size:12px; letter-spacing:0.5px;">
          📅 MINGGU KE-${week.no} ${week.monthLabel}: ${week.rangeLabel}
        </td>
      `;
      tbody.appendChild(divTr);
    }
    if (idx === TOTAL_PLC_ROWS) {
      const divTr = document.createElement('tr');
      divTr.innerHTML = `
        <td colspan="7" style="background:#fef2f2; border-top:2px dashed #dc2626; border-bottom:2px dashed #dc2626; padding:8px 12px; text-align:center; font-weight:800; color:#b91c1c; font-size:12px; letter-spacing:0.5px;">
          ⚠️ BARIS ${idx + 1} DAN SETERUSNYA TIDAK DIKIRIM KE PLC (HMI HANYA ${TOTAL_PLC_ROWS} BARIS) ⚠️
        </td>
      `;
      tbody.appendChild(divTr);
    }
    const tr = document.createElement('tr');
    const isP1 = idx === 0;
    if (isP1) tr.classList.add('row-p1-row');

    const badgeClass = isP1 ? 'row-badge-clean p1' : 'row-badge-clean';

    // Buat Datalist Khusus untuk Baris Ini (hanya model yang belum dipilih di baris lain)
    const datalistOptions = buildModelOptionsHtml(idx);
    const sutMissing = !!row.modelName && !(row.sut > 0);
    // Model yang sama boleh muncul di beberapa baris (mis. list rencana satu bulan): tidak ada peringatan duplikat.

    tr.innerHTML = `
      <td>
        <span class="${badgeClass}">Row ${row.rowNo}</span>
        <span class="reg-tag">${row.modelAddr}</span>
      </td>
      <td style="overflow: hidden; max-width: 220px;">
        <div style="position:relative;">
          <input type="text" class="input-editable model-text-input"
                 id="inputModel_${idx}" 
                 value="${escapeHtml(row.modelName)}" maxlength="20" placeholder="Ketik / Pilih Model..." 
                 list="modelDatalist_${idx}" style="width:100%; min-width:0;">
          <datalist id="modelDatalist_${idx}">
            ${datalistOptions}
          </datalist>
        </div>
      </td>
      <td style="text-align:right;">
        <input type="number" class="input-editable qty-number-input" id="inputQty_${idx}"
               value="${row.prodPlan}" min="0" max="32767" title="Register ${row.planAddr}">
      </td>
      <td style="text-align:center;">
        <input type="number" class="input-editable sut-number-input ${sutMissing ? 'input-duplicate-warn' : ''}" id="inputSut_${idx}"
               value="${row.sut > 0 ? row.sut : ''}" min="1" max="999" placeholder="-"
               title="${sutMissing ? 'SUT belum ada di Master Data Produk — isi manual' : `Register ${row.sutAddr}`}">
      </td>
      <td style="text-align:right;">
        <span class="actual-display-box" id="actualBadge_${idx}" title="Output Inventory AC OEE model ini (Read Only)">
          ${row.invActual || 0}
        </span>
      </td>
      <td style="text-align:right;">
        <span class="actual-display-box" id="defectBadge_${idx}" title="Register ${row.defAddr} (Defect - Read Only)" style="color:#b91c1c;">
          ${row.defect || 0}
        </span>
      </td>
      <td style="text-align:center;">
        <!-- TANGGAL PLAN + URUTAN ANTRIAN (kotak ditahan & digeser) -->
        <div class="queue-cell">
          <span class="queue-date ${row.planDate ? '' : 'is-empty'}" id="queueDate_${idx}">${formatPlanDate(row.planDate)}</span>
          <div class="queue-box ${row.modelName ? '' : 'is-empty'}" draggable="true" data-idx="${idx}"
               title="Tahan & geser ke baris lain untuk memindah urutan model ini">
            <span class="queue-grip">&#10303;</span><span>${idx + 1}</span>
          </div>
        </div>
      </td>
    `;

    tbody.appendChild(tr);
    attachRowDragHandlers(tr, idx);

    // Pasang Event Listeners
    const inputModel = tr.querySelector(`#inputModel_${idx}`);
    const inputQty = tr.querySelector(`#inputQty_${idx}`);
    const inputSut = tr.querySelector(`#inputSut_${idx}`);

    if (inputModel) {
      inputModel.addEventListener('input', (e) => { onModelChange(idx, e.target.value); applyInventoryActuals(); });
      inputModel.addEventListener('change', (e) => { onModelChange(idx, e.target.value); applyInventoryActuals(); });
    }

    if (inputQty) {
      inputQty.addEventListener('input', (e) => { onQtyChange(idx, e.target.value); applyInventoryActuals(); });
    }

    if (inputSut) {
      inputSut.addEventListener('input', (e) => onSutChange(idx, e.target.value));
    }
  });
}

// 5. URUTAN ANTRIAN: tahan & geser kotak urutan untuk memindah isi baris (drag & drop)
const PLAN_DAY_NAMES = ['Min', 'Sen', 'Sel', 'Rab', 'Kam', 'Jum', 'Sab'];
const PLAN_MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'Mei', 'Jun', 'Jul', 'Agu', 'Sep', 'Okt', 'Nov', 'Des'];
const PLAN_MONTH_NAMES_LONG = ['Januari', 'Februari', 'Maret', 'April', 'Mei', 'Juni', 'Juli', 'Agustus', 'September', 'Oktober', 'November', 'Desember'];

// "2026-10-06" -> "Sel, 06 Okt 2026"
function formatPlanDate(value) {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value || '');
  if (!m) return '-';
  const d = new Date(+m[1], +m[2] - 1, +m[3]);
  return `${PLAN_DAY_NAMES[d.getDay()]}, ${m[3]} ${PLAN_MONTH_NAMES[d.getMonth()]} ${m[1]}`;
}

// Minggu (Senin-Minggu, dipotong di awal/akhir bulan) dari tanggal plan "yyyy-MM-dd", sama dengan list rencana.
// null jika tanggal kosong.
function planWeekOf(value) {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value || '');
  if (!m) return null;
  const d = new Date(+m[1], +m[2] - 1, +m[3]);
  const fromMonday = (d.getDay() + 6) % 7;
  const monthStart = new Date(d.getFullYear(), d.getMonth(), 1);
  const monthEnd = new Date(d.getFullYear(), d.getMonth() + 1, 0);
  let start = new Date(d.getFullYear(), d.getMonth(), d.getDate() - fromMonday);
  let end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 6);
  if (start < monthStart) start = monthStart;
  if (end > monthEnd) end = monthEnd;
  const firstOffset = (monthStart.getDay() + 6) % 7;
  const no = Math.floor((d.getDate() - 1 + firstOffset) / 7) + 1;
  const fmt = (x) => `${PLAN_DAY_NAMES[x.getDay()]}, ${String(x.getDate()).padStart(2, '0')} ${PLAN_MONTH_NAMES[x.getMonth()]}`;
  const monthLabel = `${PLAN_MONTH_NAMES_LONG[d.getMonth()].toUpperCase()} ${d.getFullYear()}`;
  return {
    key: `${d.getFullYear()}-${d.getMonth()}-${no}`,
    no,
    monthLabel,
    rangeLabel: `${fmt(start)} s/d ${fmt(end)} ${end.getFullYear()}`
  };
}

let _dragFromIdx = null;

// Gulir otomatis saat menggeser: kursor dekat tepi atas/bawah kotak tabel (atau di luarnya) -> tabel ikut bergulir,
// makin dekat ke tepi makin cepat.
const DRAG_SCROLL_ZONE = 70;   // px dari tepi kotak tabel
const DRAG_SCROLL_MAX = 28;    // px per frame
let _dragPointerY = null;
let _dragScrollRaf = null;

document.addEventListener('dragover', (e) => {
  if (_dragFromIdx !== null) _dragPointerY = e.clientY;
});

function dragAutoScrollStep() {
  if (_dragFromIdx === null) { _dragScrollRaf = null; return; }
  const box = document.querySelector('.table-responsive-white');
  if (box && _dragPointerY !== null) {
    const rect = box.getBoundingClientRect();
    let dy = 0;
    if (_dragPointerY < rect.top + DRAG_SCROLL_ZONE) dy = -(rect.top + DRAG_SCROLL_ZONE - _dragPointerY);
    else if (_dragPointerY > rect.bottom - DRAG_SCROLL_ZONE) dy = _dragPointerY - (rect.bottom - DRAG_SCROLL_ZONE);
    if (dy !== 0) {
      const step = Math.max(-DRAG_SCROLL_MAX, Math.min(DRAG_SCROLL_MAX, Math.round(dy / DRAG_SCROLL_ZONE * DRAG_SCROLL_MAX)));
      box.scrollTop += step || Math.sign(dy);
    }
  }
  _dragScrollRaf = requestAnimationFrame(dragAutoScrollStep);
}

function startDragAutoScroll() {
  _dragPointerY = null;
  if (_dragScrollRaf === null) _dragScrollRaf = requestAnimationFrame(dragAutoScrollStep);
}

function stopDragAutoScroll() {
  if (_dragScrollRaf !== null) cancelAnimationFrame(_dragScrollRaf);
  _dragScrollRaf = null;
  _dragPointerY = null;
}

function clearDropMarkers() {
  document.querySelectorAll('#editableTableBody tr.row-drop-above, #editableTableBody tr.row-drop-below')
    .forEach(el => el.classList.remove('row-drop-above', 'row-drop-below'));
}

// Baris editor (yang punya kotak urutan) sesuai indeks
function editorRowElements() {
  return [...document.querySelectorAll('#editableTableBody tr')].filter(t => t.querySelector('.queue-box'));
}

// Animasi setelah dilepas: tiap baris meluncur dari posisi lamanya ke posisi baru, baris yang dipindah berkedip.
function animateRowMove(from, to, oldTops) {
  const rows = editorRowElements();
  const lo = Math.min(from, to), hi = Math.max(from, to);
  for (let j = lo; j <= hi; j++) {
    // isi baris j sekarang berasal dari baris "src" sebelum dipindah
    const src = j === to ? from : (from < to ? j + 1 : j - 1);
    const tr = rows[j];
    if (!tr || oldTops[src] === undefined) continue;
    const dy = oldTops[src] - tr.getBoundingClientRect().top;
    if (!dy) continue;
    tr.style.transition = 'none';
    tr.style.transform = `translateY(${dy}px)`;
    tr.style.position = 'relative';
    tr.style.zIndex = j === to ? '3' : '1';
  }
  // Paksa browser menghitung posisi awal (reflow), lalu lepaskan transform dengan transisi -> baris meluncur
  if (rows[lo]) void rows[lo].offsetHeight;
  for (let j = lo; j <= hi; j++) {
    const tr = rows[j];
    if (!tr) continue;
    tr.style.transition = 'transform 380ms cubic-bezier(0.2, 0.8, 0.2, 1)';
    tr.style.transform = '';
  }
  if (rows[to]) rows[to].classList.add('row-just-moved');
  setTimeout(() => {
    for (let j = lo; j <= hi; j++) {
      const tr = rows[j];
      if (!tr) continue;
      // Pengaman: transisi dipaksa selesai (mis. tab sempat tidak terlihat) agar baris tidak tertinggal di posisi geser
      if (tr.getAnimations) tr.getAnimations().forEach(a => a.finish());
      tr.style.transition = ''; tr.style.transform = ''; tr.style.position = ''; tr.style.zIndex = '';
    }
  }, 450);
  setTimeout(() => { if (rows[to]) rows[to].classList.remove('row-just-moved'); }, 1700);
}

function attachRowDragHandlers(tr, idx) {
  const box = tr.querySelector('.queue-box');
  if (box) {
    box.addEventListener('dragstart', (e) => {
      _dragFromIdx = idx;
      e.dataTransfer.effectAllowed = 'move';
      e.dataTransfer.setData('text/plain', String(idx));
      // Label melayang: model yang sedang dibawa
      const row = current9Rows[idx] || {};
      const ghost = document.createElement('div');
      ghost.className = 'drag-ghost';
      ghost.textContent = `⠿ Urutan ${idx + 1} · ${row.modelName || '(kosong)'}${row.prodPlan ? ' · ' + row.prodPlan + ' pcs' : ''}${row.planDate ? ' · ' + formatPlanDate(row.planDate) : ''}`;
      document.body.appendChild(ghost);
      e.dataTransfer.setDragImage(ghost, 18, 16);
      setTimeout(() => ghost.remove(), 0);
      tr.classList.add('row-dragging');
      startDragAutoScroll();
    });
    box.addEventListener('dragend', () => {
      _dragFromIdx = null;
      stopDragAutoScroll();
      tr.classList.remove('row-dragging');
      clearDropMarkers();
    });
  }

  // Seluruh baris jadi area tujuan: garis biru di atas/bawah menunjukkan posisi sisipan
  tr.addEventListener('dragover', (e) => {
    if (_dragFromIdx === null) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    const rect = tr.getBoundingClientRect();
    const below = e.clientY > rect.top + rect.height / 2;
    let to = below ? idx + 1 : idx;
    if (_dragFromIdx < to) to -= 1;
    const cls = below ? 'row-drop-below' : 'row-drop-above';
    const lastTd = tr.lastElementChild;
    if (tr.classList.contains(cls) && lastTd && lastTd.dataset.dropTo === String(to)) return; // tidak berubah
    clearDropMarkers();
    if (to === _dragFromIdx) return; // posisi sama dengan asal: tidak ada perpindahan
    tr.classList.add(cls);
    if (lastTd) {
      lastTd.dataset.dropTo = String(to);
      lastTd.dataset.dropLabel = `Taruh di sini → urutan ${to + 1}`;
    }
  });
  tr.addEventListener('drop', (e) => {
    if (_dragFromIdx === null) return;
    e.preventDefault();
    const rect = tr.getBoundingClientRect();
    const below = e.clientY > rect.top + rect.height / 2;
    const from = _dragFromIdx;
    let to = below ? idx + 1 : idx;
    if (from < to) to -= 1; // setelah baris asal dikeluarkan, indeks di bawahnya bergeser naik
    clearDropMarkers();
    _dragFromIdx = null;
    stopDragAutoScroll();
    if (from === to) return;
    const scrollBox = document.querySelector('.table-responsive-white');
    const keepScroll = scrollBox ? scrollBox.scrollTop : 0;
    const oldTops = editorRowElements().map(el => el.getBoundingClientRect().top);
    moveEditorRow(from, to);
    if (scrollBox) scrollBox.scrollTop = keepScroll; // tabel digambar ulang: posisi gulir dipertahankan
    animateRowMove(from, to, oldTops);
  });
}

// Pindahkan isi baris (Model, Plan, SUT, Tanggal, Actual & Defect tersimpan) dari posisi "from" ke "to";
// baris di antaranya ikut bergeser. Register PLC (dan actual register untuk preview) tetap di barisnya.
function moveEditorRow(from, to) {
  if (from === to || from < 0 || to < 0 || from >= current9Rows.length || to >= current9Rows.length) return;
  const contents = current9Rows.map(r => ({ modelName: r.modelName, prodPlan: r.prodPlan, sut: r.sut, planDate: r.planDate, invActual: r.invActual, defect: r.defect }));
  const [moved] = contents.splice(from, 1);
  contents.splice(to, 0, moved);
  current9Rows.forEach((r, i) => Object.assign(r, contents[i]));

  renderEditableTable();
  renderHmiPreview();
  addLog(`[URUTAN] "${moved.modelName || '(kosong)'}" dipindah dari urutan ${from + 1} ke urutan ${to + 1}.`, 'info');
}

// 6. HANDLER SAAT OPERATOR MENGETIK / MEMILIH NAMA MODEL
window.onModelChange = function(idx, val) {
  if (!current9Rows[idx]) return;
  const trimmed = val.trim();
  if (plcModelKey(trimmed) !== plcModelKey(current9Rows[idx].modelName)) {
    // Model baris diganti: DEFECT tersimpan milik model lama tidak berlaku lagi
    current9Rows[idx].defect = 0;
    const defBadge = document.getElementById(`defectBadge_${idx}`);
    if (defBadge) defBadge.innerText = 0;
  }
  current9Rows[idx].modelName = trimmed;

  if (!trimmed) {
    current9Rows[idx].prodPlan = 0;
    current9Rows[idx].sut = 0;
    current9Rows[idx].planDate = '';
    const dateEl = document.getElementById(`queueDate_${idx}`);
    if (dateEl) { dateEl.textContent = '-'; dateEl.classList.add('is-empty'); }
    const inputQty = document.getElementById(`inputQty_${idx}`);
    if (inputQty) inputQty.value = 0;
    setSutInput(idx, 0, false);
    renderHmiPreview();
    updateAllDatalists();
    return;
  }

  // Cari model HANYA di database tanggal yang dipilih
  let matched = rawSapPlanItems.find(x => 
    x.productName.toUpperCase() === trimmed.toUpperCase() &&
    x.machineCode === currentMachine &&
    (currentShift === 'ALL' || x.shift.toUpperCase() === currentShift.toUpperCase())
  );

  if (!matched) {
    matched = rawSapPlanItems.find(x => 
      x.productName.toUpperCase() === trimmed.toUpperCase() &&
      (currentShift === 'ALL' || x.shift.toUpperCase() === currentShift.toUpperCase())
    );
  }

  if (!matched) {
    matched = rawSapPlanItems.find(x => 
      x.productName.toUpperCase() === trimmed.toUpperCase()
    );
  }

  // Plan dari rencana SapPlan (jika model ada di rencana tanggal ini)
  if (matched) {
    const qty = getProductPlanQuantity(matched);
    current9Rows[idx].prodPlan = qty;
    const inputQty = document.getElementById(`inputQty_${idx}`);
    if (inputQty) inputQty.value = qty;
  }

  // SUT dari Master Data Produk (kosong jika model belum terdaftar)
  const sut = getMasterSut(trimmed);
  current9Rows[idx].sut = sut;
  setSutInput(idx, sut, sut <= 0);

  if (matched || sut > 0) {
    addLog(`[AUTO-FILL] Row ${idx + 1}: Model "${trimmed}" -> Plan: ${matched ? getProductPlanQuantity(matched) + ' pcs' : '(isi manual)'}, SUT: ${sut > 0 ? sut + 's' : 'belum ada di Master Data Produk'}.`, sut > 0 ? 'info' : 'error');
  }

  renderHmiPreview();
  updateAllDatalists();
};

// Isi kotak SUT satu baris + tandai merah jika SUT belum ada di Master Data Produk
function setSutInput(idx, sut, warnMissing) {
  const inputSut = document.getElementById(`inputSut_${idx}`);
  if (!inputSut) return;
  inputSut.value = sut > 0 ? sut : '';
  inputSut.classList.toggle('input-duplicate-warn', warnMissing);
  inputSut.title = warnMissing
    ? 'SUT belum ada di Master Data Produk — isi manual'
    : `Register ${current9Rows[idx].sutAddr}`;
}

// Saran model untuk satu baris editor:
// 1. Model dari rencana SapPlan (tanggal & shift terpilih) -> lengkap dengan Plan
// 2. Model CU di Master Data Produk yang tidak ada di rencana
// Model yang sudah dipakai di baris lain tidak ditampilkan.
function buildModelOptionsHtml(idx) {
  // Semua model tetap ditawarkan walau sudah dipakai di baris lain (model yang sama boleh di beberapa baris)
  const seen = new Set();
  const options = [];

  getFilteredPlanItems().forEach(p => {
    const key = (p.productName || '').trim().toUpperCase();
    if (!key || seen.has(key)) return;
    seen.add(key);
    const sut = getMasterSut(p.productName);
    options.push(`<option value="${escapeHtml(p.productName)}">${escapeHtml(p.productName)} - Plan: ${getProductPlanQuantity(p)} pcs | SUT: ${sut > 0 ? sut + 's' : 'belum ada'} (Shift ${escapeHtml(p.shift)})</option>`);
  });

  masterModelList.forEach(m => {
    const key = m.model.toUpperCase();
    if (!key.startsWith('CU') || seen.has(key)) return;
    seen.add(key);
    options.push(`<option value="${escapeHtml(m.model)}">${escapeHtml(m.model)} - Master Data | SUT: ${m.sut}s</option>`);
  });

  return options.join('');
}

// Memperbarui opsi Datalist di semua baris (saran model dari rencana & Master Data Produk)
function updateAllDatalists() {
  for (let idx = 0; idx < current9Rows.length; idx++) {
    const datalist = document.getElementById(`modelDatalist_${idx}`);
    if (datalist) datalist.innerHTML = buildModelOptionsHtml(idx);
  }
}

// Handler Saat Quantity Diedit Manual
window.onQtyChange = function(idx, val) {
  if (current9Rows[idx]) {
    const num = parseInt(val) || 0;
    current9Rows[idx].prodPlan = num;
  }
};

// Handler Saat SUT Diedit Manual
window.onSutChange = function(idx, val) {
  if (current9Rows[idx]) {
    const num = parseInt(val) || 0;
    current9Rows[idx].sut = num;
    const inputSut = document.getElementById(`inputSut_${idx}`);
    if (inputSut) inputSut.classList.toggle('input-duplicate-warn', !!current9Rows[idx].modelName && num <= 0);
  }
};

// Switch Halaman Tampilan HMI GOT (1: Row 1-8, 2: Row 9-16, 3: Row 17-24, 4: Row 25-32, 5: Row 33-40, 6: Row 41-48)
window.switchHmiPage = function(page) {
  currentHmiPage = page;
  const titleEl = document.getElementById('hmiExactTitleText');
  if (titleEl) {
    titleEl.innerText = `PLAN PRODUCTION ${page}`;
  }
  renderHmiPreview();
};


// Isi Live Preview = cermin (mirror) register PLC, BUKAN isi editor.
// Diperbarui dari api/plc/read setiap 3 detik; saat PLC offline semua nilai tampil "-".
let plcMirrorRows = [];
let plcMirrorLive = null; // null = belum pernah terbaca, true = online, false = offline

function getPlcMirrorRows() {
  if (plcMirrorRows.length === 0) {
    plcMirrorRows = PLC_REGISTER_MAP.map((reg, i) => ({ ...makeEditorRow(i, '', 0, 0), actual: 0, defect: 0 }));
  }
  return plcMirrorRows;
}

function setPlcMirrorStatus(state, readAt) {
  const el = document.getElementById('hmiMirrorStatus');
  if (!el) return;
  const styles = {
    live:    ['#dcfce7', '#166534', `● MIRROR PLC ONLINE — terbaca ${readAt || ''}`],
    offline: ['#fee2e2', '#991b1b', `● PLC OFFLINE — preview tidak menampilkan data${readAt ? ' (cek ' + readAt + ')' : ''}`],
    server:  ['#fef3c7', '#92400e', '● Server PLC Dispatcher tidak merespons'],
    wait:    ['#e2e8f0', '#334155', '● Menghubungkan ke PLC...']
  };
  const [bg, fg, text] = styles[state] || styles.wait;
  el.style.background = bg;
  el.style.color = fg;
  el.innerText = text;
}

// Render Live Preview Layar HMI GOT (8 baris per halaman: Plan Production 1 - 15)
function renderHmiPreview() {
  const tbody = document.getElementById('hmiPreviewBody');
  if (!tbody) return;
  tbody.innerHTML = '';

  const pagePos = Math.max(0, HMI_PAGES.indexOf(currentHmiPage));
  const startIdx = pagePos * ROWS_PER_HMI_PAGE;
  const endIdx = startIdx + ROWS_PER_HMI_PAGE;
  const live = plcMirrorLive === true;
  const displayRows = getPlcMirrorRows().slice(startIdx, endIdx).map(r => live ? r : { ...r, modelName: '', prodPlan: '-', sut: 0, actual: '-', defect: '-' });

  displayRows.forEach((row, rowSubIdx) => {
    const idx = startIdx + rowSubIdx;
    const tr = document.createElement('tr');
    const modelDisplay = row.modelName || '-';

    tr.innerHTML = `
      <td style="padding: 2px;">
        <div class="hmi-cell-lcd no-cell">
          <span class="hmi-cell-lcd-tag">${row.noId || '10089'} ${row.noAddr || 'R999'}</span>
          <span class="hmi-cell-lcd-val">${row.rowNo}</span>
        </div>
      </td>
      <td style="padding: 2px;">
        <div class="hmi-cell-lcd model-cell">
          <span class="hmi-cell-lcd-tag">${row.mId || '10005'} ${row.modelAddr}</span>
          <span class="hmi-cell-lcd-val" id="hmiModelText_${idx}" title="${escapeHtml(modelDisplay)}">${escapeHtml(modelDisplay)}</span>
        </div>
      </td>
      <td style="padding: 2px;">
        <div class="hmi-cell-lcd num-right">
          <span class="hmi-cell-lcd-tag">${row.pId || '10037'} ${row.planAddr}</span>
          <span class="hmi-cell-lcd-val" id="hmiQtyText_${idx}" title="Nilai PROD. PLAN di PLC">${row.prodPlan}</span>
        </div>
      </td>
      <td style="padding: 2px;">
        <div class="hmi-cell-lcd num-right">
          <span class="hmi-cell-lcd-tag">${row.sId || '10045'} ${row.sutAddr || row.sAddr || 'R1011'}</span>
          <span class="hmi-cell-lcd-val" id="hmiSutText_${idx}">${row.sut > 0 ? row.sut + ' s' : '-'}</span>
        </div>
      </td>
      <td style="padding: 2px;">
        <div class="hmi-cell-lcd num-right">
          <span class="hmi-cell-lcd-tag">${row.actId || '10064'} ${row.actAddr}</span>
          <span class="hmi-cell-lcd-val" id="hmiActText_${idx}" title="Nilai ACTUAL ditarik dari PLC">${row.actual}</span>
        </div>
      </td>
      <td style="padding: 2px;">
        <div class="hmi-cell-lcd num-right">
          <span class="hmi-cell-lcd-tag">${row.defId || '10072'} ${row.defAddr}</span>
          <span class="hmi-cell-lcd-val" id="hmiDefText_${idx}" title="Nilai DEFECT ditarik dari PLC">${row.defect ?? 0}</span>
        </div>
      </td>
    `;

    tbody.appendChild(tr);
  });

  // Perbarui Navigasi Tombol GOT Bawah (Home & Arrow Page)
  const navContainer = document.getElementById('hmiBottomNavBar');
  if (navContainer) {
    const arrowStyle = 'background:#0284c7; color:white; border:2px solid #38bdf8; border-radius:4px; padding:3px 14px; font-weight:bold; cursor:pointer; font-size:14px;';
    const prevPage = HMI_PAGES[pagePos - 1];
    const nextPage = HMI_PAGES[pagePos + 1];
    const leftHtml = prevPage ? `
        <div class="hmi-nav-left" style="display: flex; flex-direction: column; align-items: center;">
          <span class="hmi-home-tag">${currentHmiPage === 2 ? '10064' : '&nbsp;'}</span>
          <button type="button" class="hmi-btn-arrow-got" onclick="switchHmiPage(${prevPage})" title="Kembali ke Layar PLAN PRODUCTION ${prevPage}" style="${arrowStyle}">◀</button>
        </div>` : `<div style="visibility: hidden;"><span class="hmi-home-tag">&nbsp;</span><button type="button" style="${arrowStyle}" tabindex="-1">◀</button></div>`;
    const rightHtml = nextPage ? `
        <div class="hmi-nav-right" style="display: flex; flex-direction: column; align-items: center;">
          <span class="hmi-home-tag">${currentHmiPage === 1 ? '10109' : '&nbsp;'}</span>
          <button type="button" class="hmi-btn-arrow-got" onclick="switchHmiPage(${nextPage})" title="Lanjut ke Layar PLAN PRODUCTION ${nextPage}" style="${arrowStyle}">▶</button>
        </div>` : `<div style="visibility: hidden;"><span class="hmi-home-tag">&nbsp;</span><button type="button" style="${arrowStyle}" tabindex="-1">▶</button></div>`;
    navContainer.innerHTML = `
        ${leftHtml}
        <div class="hmi-home-center" style="display: flex; flex-direction: column; align-items: center;">
          <span class="hmi-home-tag">${currentHmiPage === 1 ? '10096' : currentHmiPage === 2 ? '10063' : '&nbsp;'}</span>
          <button type="button" class="hmi-btn-home-got" disabled title="Fitur HOME dinonaktifkan" style="opacity: 0.5; cursor: not-allowed;">HOME</button>
        </div>
        ${rightHtml}
      `;
  }
}

// Tombol "Submit & Kirim ke PLC" (header): simpan isi editor ke database sekarang juga (tanpa menunggu simpan
// otomatis), lalu kirim ke PLC dengan fungsi yang sama seperti tombol "KONFIRMASI & KIRIM KE PLC".
window.submitAndDispatch = async function() {
  const filled = editorDraftRows().length;
  if (filled === 0) {
    showAppModal({
      type: 'info',
      title: 'Editor Masih Kosong',
      message: 'Belum ada baris yang diisi. Isi editor dulu (mis. lewat <strong>Isi dari Rencana Database</strong>).',
      buttonText: 'Mengerti'
    });
    return;
  }
  if (!confirm(`Submit ${filled} baris ke database lalu kirim ke PLC (Plan 1 - ${LAST_HMI_PAGE})?`)) return;

  const btn = document.getElementById('btnSubmitDispatch');
  if (btn) btn.disabled = true;
  try {
    // 1. Simpan ke database (hanya jika ada perubahan yang belum tersimpan)
    const sig = JSON.stringify(editorDraftRows());
    if (_draftLoaded && sig !== _draftSavedSig) {
      while (_draftBusy) await new Promise(r => setTimeout(r, 100));
      await saveEditorDraft(sig);
    }
    if (!_draftLoaded || _draftSavedSig !== sig) {
      addLog('[SUBMIT] Gagal menyimpan ke database. Pengiriman ke PLC dibatalkan.', 'error');
      showAppModal({
        type: 'error',
        title: 'Gagal Menyimpan',
        message: 'Isi editor tidak bisa disimpan ke database, jadi <strong>belum dikirim ke PLC</strong>.',
        tip: 'Periksa koneksi database lalu klik Submit lagi. Tombol KONFIRMASI & KIRIM KE PLC di bawah tetap bisa mengirim tanpa database.',
        buttonText: 'Tutup'
      });
      return;
    }
    addLog(`[SUBMIT] ${filled} baris tersimpan di database. Mengirim ke PLC...`, 'info');

    // 2. Kirim ke PLC (sama dengan tombol KONFIRMASI & KIRIM KE PLC)
    await dispatchToPlc(false);
  } finally {
    if (btn) btn.disabled = false;
  }
};

// Fitur HOME (tulis D1000 = 0 ke PLC) DINONAKTIFKAN: tombol tetap tampil tapi disabled, endpoint backend sudah dihapus.

// Konfirmasi & Kirim ke PLC Mitsubishi MC Protocol (16 Baris: Plan 1 & Plan 2)
async function dispatchToPlc(row1Only = false) {
  const btnAll = document.getElementById('btnConfirmDispatchAll');
  const btnP1 = document.getElementById('btnConfirmDispatchP1');
  if (btnAll) btnAll.disabled = true;
  if (btnP1) btnP1.disabled = true;

  try {
    const targetRows = row1Only ? current9Rows.slice(0, 1) : current9Rows.slice(0, TOTAL_PLC_ROWS);
    const label = row1Only ? 'Row 1 (Prioritas)' : `${targetRows.length} Baris Rencana Produksi (Plan 1 - ${LAST_HMI_PAGE})`;

    // Tidak ada SUT default: baris bermodel wajib punya SUT sebelum dikirim ke PLC
    const noSut = targetRows.filter(r => r.modelName && !(r.sut > 0));
    if (noSut.length > 0) {
      const list = noSut.map(r => `Row ${r.rowNo} (${escapeHtml(r.modelName)})`).join(', ');
      addLog(`[DITAHAN] SUT kosong pada: ${noSut.map(r => `Row ${r.rowNo} (${r.modelName})`).join(', ')}. Data belum dikirim ke PLC.`, 'error');
      showAppModal({
        type: 'warning',
        title: 'SUT Belum Diisi',
        badge: 'Belum Dikirim',
        message: `Baris berikut sudah punya model tapi SUT-nya kosong: <strong>${list}</strong>.`,
        tip: 'Isi SUT manual di editor, atau tambahkan modelnya di halaman Master Data Produk lalu pilih ulang modelnya.',
        buttonText: 'Mengerti'
      });
      return;
    }

    addLog(`[KONFIRMASI] Operator mengonfirmasi pengiriman ${label} ke PLC...`, 'info');

    const rowsPayload = targetRows.map(r => ({
      RowNo: r.rowNo,
      ModelNameAddress: r.modelAddr,
      ModelNameLength: 20,
      ProdPlanAddress: r.planAddr,
      SutAddress: r.sutAddr,
      ActualAddress: r.actAddr, // Read-Only
      DefectAddress: r.defAddr, // Read-Only
      ModelName: r.modelName || '',
      ProdPlan: r.prodPlan || 0,
      Sut: r.sut || 0
    }));

    const payload = {
      Mode: row1Only ? 'row1_only' : 'all',
      Rows: rowsPayload,
      ConfirmedBy: 'Operator Web'
    };

    const res = await fetch('api/plc/send', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const result = await res.json();

    if (result.success) {
      addLog(`[BERHASIL] ${result.message}`, 'success');
      if (result.logs) {
        result.logs.forEach(l => {
          if (l.includes('[VALIDASI OK]')) {
            addLog(l, 'success');
          } else if (l.includes('[GAGAL]') || l.includes('[VALIDASI PERINGATAN]')) {
            addLog(l, 'error');
          } else {
            addLog(l, 'info');
          }
        });
      }
      
      const valCount = (result.logs || []).filter(l => l.includes('[VALIDASI OK]')).length;
      showAppModal({
        type: 'success',
        title: 'Sukses Terkirim & Tervalidasi',
        badge: '100% Cocok di PLC',
        message: `Sebanyak <strong>${valCount} baris</strong> data rencana produksi berhasil dikirim dan <strong>100% TERVERIFIKASI</strong> cocok dengan memori register PLC (Plan 1 - ${LAST_HMI_PAGE}).<br><div style="margin-top:8px;padding:6px 10px;background:rgba(16,185,129,0.15);border-radius:6px;border:1px solid #10b981;font-size:12px;color:#10b981;"><strong>⚡ Register D50 = 1</strong> berhasil dikirim (Handshake / Trigger Plan Baru).</div>`,
        tip: 'Layar fisik GOT HMI telah terisi data terbaru. Anda dapat memeriksa rincian tiap baris pada tab Riwayat Aktivitas.',
        buttonText: 'Selesai'
      });
    } else {
      addLog(`[PERINGATAN] ${result.message}`, 'error');
      showAppModal({
        type: 'warning',
        title: 'Peringatan Pengiriman PLC',
        badge: 'Periksa PLC',
        message: result.message,
        tip: 'Periksa status koneksi kabel LAN atau port PLC 192.168.1.30:5010.',
        buttonText: 'Mengerti'
      });
    }

    const plcStatusEl = document.getElementById('plcStatusText');
    if (plcStatusEl) plcStatusEl.innerText = result.plcStatus || 'PLC Ready';

    readPlcRegisters(true);
  } catch (err) {
    addLog(`[ERROR] Terjadi kesalahan saat mengirim ke PLC: ${err.message}`, 'error');
    showAppModal({
      type: 'error',
      title: 'Gagal Menghubungi PLC',
      badge: 'Koneksi Terputus',
      message: `Terjadi kendala saat berkomunikasi dengan PLC: <strong>${err.message}</strong>`,
      tip: 'Pastikan server aplikasi berjalan, kabel LAN tersambung ke PLC, dan IP 192.168.1.30 dapat dijangkau.',
      buttonText: 'Tutup'
    });
  } finally {
    if (btnAll) btnAll.disabled = false;
    if (btnP1) btnP1.disabled = false;
  }
}

let _isPollingPlc = false;
let _plcActualPollingInterval = null;
let _currentPlcDataVersion = -1; // Versi snapshot data PLC yang dipegang browser

// Menarik Nilai Aktual dan Defect dari PLC (GET VALUE & CONDITIONAL CHANGE-DETECTION)
async function readPlcRegisters(silent = false) {
  if (_isPollingPlc && silent) return; // cegah request bertumpuk saat silent polling
  _isPollingPlc = true;

  try {
    if (!silent) addLog('[PLC GET VALUE] Menarik data register ACTUAL (R1012..R1952) & DEFECT (R1014..R1954) dari PLC...', 'info');
    
    // Kirim versi data terakhir: jika di PLC tidak ada perubahan, server kirim { changed: false } super hemat bandwidth!
    let url = `api/plc/read?v=${_currentPlcDataVersion}`;
    if (!silent) url += '&log=1';

    const res = await fetch(url);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    if (!data.version) { setPlcMirrorStatus('wait'); return; } // server belum selesai membaca PLC pertama kali
    setPlcMirrorStatus(data.readLive ? 'live' : 'offline', data.readAt);

    // JIKA TIDAK ADA PERUBAHAN DI PLC:
    if (data.changed === false) {
      // Tidak ada perubahan sama sekali di memori PLC -> hemat bandwidth & CPU browser (tidak perlu render ulang)
      return;
    }

    // JIKA ADA PERUBAHAN DI PLC:
    _currentPlcDataVersion = data.version;

    // Live Preview = cermin isi PLC (Model, Plan, SUT, Actual, Defect)
    plcMirrorLive = !!data.readLive;
    const mirror = getPlcMirrorRows();
    (data.rows || []).forEach(r => {
      const m = mirror.find(x => x.rowNo === r.rowNo);
      if (!m) return;
      m.modelName = (r.modelName || '').trim();
      m.prodPlan = r.prodPlan || 0;
      m.sut = r.sut || 0;
      m.actual = r.actual || 0;
      m.defect = r.defect || 0;
    });
    renderHmiPreview();

    if (data.rows && data.rows.length > 0) {
      let changeCount = 0;
      data.rows.forEach(r => {
        const fIdx = current9Rows.findIndex(x => x.rowNo === r.rowNo);
        const found = current9Rows[fIdx];
        if (found) {
          // ACTUAL register PLC hanya untuk Live Preview; kolom ACTUAL editor diisi dari Inventory (applyInventoryActuals)
          found.actual = r.actual;

          // DEFECT register PLC hanya dicatat ke baris editor yang modelnya sama dengan model di PLC.
          // Baris PLC sudah berganti model (atau belum dikirim) = DEFECT tersimpan baris ini dipertahankan.
          const sameModel = !!found.modelName && plcModelKey(r.modelName) === plcModelKey(found.modelName);
          if (!sameModel) return;
          const defChanged = (found.defect || 0) !== (r.defect || 0);
          if (defChanged) {
            changeCount++;
          }
          found.defect = r.defect || 0;

          const defBadge = document.getElementById(`defectBadge_${fIdx}`);
          if (defBadge) {
            defBadge.innerText = r.defect || 0;
            if (defChanged && silent) {
              defBadge.classList.add('badge-defect-flash');
              setTimeout(() => defBadge.classList.remove('badge-defect-flash'), 1000);
            }
          }
        }
      });

      if (changeCount > 0 && silent) {
        addLog(`[PLC AUTO-SYNC] Nilai DEFECT berubah di PLC: ${changeCount} baris diperbarui otomatis.`, 'info');
      }

      if (!silent) {
        addLog(`[PLC BACA] Berhasil menarik nilai ACTUAL & DEFECT dari PLC (${data.rows.length} baris).`, 'success');
        showAppModal({
          type: 'info',
          title: 'Nilai Aktual & Defect Terbaca',
          badge: 'Status PLC: ' + (data.plcStatus || 'Online'),
          message: `Nilai <strong>ACTUAL (R1012..R1952)</strong> dan <strong>DEFECT (R1014..R1954)</strong> untuk <strong>${data.rows.length} baris</strong> berhasil diperbarui langsung dari memori fisik PLC.`,
          tip: 'Preview layar HMI GOT dan kolom DEFECT sudah disinkronkan. Kolom ACTUAL tabel editor diambil dari output Inventory AC OEE per model.',
          buttonText: 'Tutup'
        });
      }
    }
  } catch (err) {
    setPlcMirrorStatus('server');
    if (!silent) addLog(`[PLC ERROR] Gagal membaca dari PLC: ${err.message}`, 'error');
  } finally {
    _isPollingPlc = false;
  }
}

// Mulai Polling Otomatis Aktual & Defect dari PLC (Setiap 3 Detik)
function startPlcActualPolling() {
  if (_plcActualPollingInterval) clearInterval(_plcActualPollingInterval);
  // Delay pertama 2 detik setelah halaman dimuat
  setTimeout(() => readPlcRegisters(true), 2000);
  // Polling terus setiap 3 detik
  _plcActualPollingInterval = setInterval(() => {
    readPlcRegisters(true);
  }, 3000);
}

// =========================================================
// ACTUAL EDITOR = OUTPUT INVENTORY AC OEE PER MODEL (dbo.PlcKyoshinDailyOutput, akumulasi R22 per hari produksi)
// Output model sejak tanggal plan paling awal di editor tetap berada di kemunculan pertama model pada antrean.
// Dengan begitu, hasil aktual yang melebihi Prod. Plan tetap tercatat pada rencana produksi yang menjalankannya,
// bukan terbagi ke antrean model yang sama pada tanggal berikutnya. Diperbarui setiap 60 detik.
// =========================================================
let _inventoryOutput = { from: null, rows: [] };
let _inventoryLoading = false;

const inventoryModelKey = (name) => (name || '').trim().toUpperCase();

// Tanggal plan paling awal di editor; tanpa tanggal plan = awal bulan berjalan
function inventoryActualFrom() {
  const dates = current9Rows.filter(r => r.modelName && /^\d{4}-\d{2}-\d{2}$/.test(r.planDate || '')).map(r => r.planDate).sort();
  return dates.length ? dates[0] : getTodayLocalDate().slice(0, 8) + '01';
}

function computeInventoryActuals() {
  const from = inventoryActualFrom();
  // Editor berisi tanggal plan lebih awal dari data yang sudah diambil: ambil ulang (mis. setelah isi editor dimuat)
  if (_inventoryOutput.from && from < _inventoryOutput.from && !_inventoryLoading) loadInventoryActuals(false);
  // Data Inventory belum pernah terbaca: ACTUAL tersimpan di database dipertahankan (tidak diubah jadi 0)
  if (!_inventoryOutput.from) return;
  const left = new Map();
  _inventoryOutput.rows.forEach(r => {
    if (r.date < from) return;
    const k = inventoryModelKey(r.model);
    left.set(k, (left.get(k) || 0) + (r.actual || 0));
  });
  const firstIdx = new Map();
  current9Rows.forEach((r, i) => {
    if (!r.modelName) return;
    const k = inventoryModelKey(r.modelName);
    if (!firstIdx.has(k)) firstIdx.set(k, i);
  });
  current9Rows.forEach((r, i) => {
    if (!r.modelName) { r.invActual = 0; return; }
    const k = inventoryModelKey(r.modelName);
    const remaining = left.get(k) || 0;
    const take = firstIdx.get(k) === i ? remaining : 0;
    r.invActual = take;
    left.set(k, remaining - take);
  });
}

// Hitung ulang & perbarui angka di tabel tanpa menggambar ulang tabel
function applyInventoryActuals(flash = false) {
  const before = current9Rows.map(r => r.invActual || 0);
  computeInventoryActuals();
  current9Rows.forEach((r, i) => {
    const badge = document.getElementById(`actualBadge_${i}`);
    if (!badge || before[i] === (r.invActual || 0)) return;
    badge.innerText = r.invActual || 0;
    if (flash) {
      badge.classList.add('badge-updated-flash');
      setTimeout(() => badge.classList.remove('badge-updated-flash'), 1000);
    }
  });
}

async function loadInventoryActuals(flash = false) {
  const from = inventoryActualFrom();
  _inventoryLoading = true;
  try {
    const res = await fetch(`api/inventory-output?machine=${encodeURIComponent(currentMachine)}&from=${from}`);
    const data = await res.json();
    if (!res.ok || !data.success) throw new Error(data.message || `HTTP ${res.status}`);
    _inventoryOutput = {
      from,
      rows: (data.rows || []).map(r => ({ date: r.date, model: r.model, actual: r.actual }))
    };
    applyInventoryActuals(flash);
  } catch (err) {
    console.warn('[INVENTORY ACTUAL]', err.message); // angka terakhir dipertahankan
  } finally {
    _inventoryLoading = false;
  }
}

function startInventoryActualPolling() {
  setTimeout(() => loadInventoryActuals(false), 1500);
  setInterval(() => loadInventoryActuals(true), 60000);
}

// Activity Log
function addLog(msg, type = 'info') {
  const logBox = document.getElementById('feedbackLogBox');
  if (!logBox) return;

  const div = document.createElement('div');
  const time = new Date().toLocaleTimeString('id-ID', { hour12: false });
  div.innerText = `[${time}] ${msg}`;

  if (type === 'success') div.className = 'log-success';
  else if (type === 'error') div.className = 'log-error';
  else if (type === 'info') div.className = 'log-info';
  else div.className = 'log-muted';

  logBox.appendChild(div);
  logBox.scrollTop = logBox.scrollHeight;
}

// Escape HTML
function escapeHtml(text) {
  if (!text) return '';
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}


// =========================================================================
// POLLING: BACA R100 (SHIFT) & R101 (HARI KERJA/LIBUR) DARI PLC
// =========================================================================

async function startPlcStatusPolling() {
  await fetchPlcStatus(); // langsung fetch pertama kali
  setInterval(fetchPlcStatus, 15000); // lalu setiap 15 detik
}

async function fetchPlcStatus() {
  try {
    const res = await fetch('api/plc/status');
    if (!res.ok) return;
    const st = await res.json();

    // Update badge Shift & Status
    const r100El = document.getElementById('r100Val');
    const r100Badge = document.getElementById('r100Badge');
    if (r100El) {
      r100El.innerText = st.shiftName ? st.shiftName.split(' ')[0] : 'Normal';
    }
    if (r100Badge) {
      r100Badge.className = 'r-register-pill';
      r100Badge.style.opacity = '1';
    }

    // Update badge R101 (Hari Kerja/Libur)
    const r101El = document.getElementById('r101Val');
    const r101Badge = document.getElementById('r101Badge');
    if (r101El) r101El.innerText = st.dayName || 'Hari Kerja';
    if (r101Badge) {
      r101Badge.className = st.r101 === 2 ? 'r-register-pill libur' : 'r-register-pill';
    }

    // Update badge Overtime
    const otBadge = document.getElementById('overtimeBadge');
    if (otBadge) {
      if (st.isOvertime) {
        otBadge.style.display = 'inline-flex';
        otBadge.className = 'r-register-pill overtime';
        otBadge.innerHTML = st.r101 === 2
          ? '&#9201; OVERTIME - Hari Libur'
          : '&#9201; OVERTIME - Di Luar Jam Shift';
      } else {
        otBadge.style.display = 'none';
      }
    }

    // Update tabel jadwal shift - highlight baris yang aktif berdasarkan waktu server
    const activeShiftVal = (st.shiftName || '').includes('Shift 1') ? 1
                         : (st.shiftName || '').includes('Shift 2') ? 2
                         : (st.shiftName || '').includes('Shift 3') ? 3
                         : (st.shiftName || '').includes('Non-Shift') ? 4 : 1;

    [1, 2, 3, 4].forEach(val => {
      const row = document.getElementById(`shiftRow_${val}`);
      const statusCell = document.getElementById(`shiftStatus_${val}`);
      if (!row || !statusCell) return;

      if (activeShiftVal === val) {
        row.classList.add('shift-row-active');
        if (st.isOvertime) {
          statusCell.innerHTML = `<span class="shift-status-pill overtime">&#9201; Overtime (Lembur)</span>`;
        } else {
          statusCell.innerHTML = `<span class="shift-status-pill active">&#9989; Shift Berjalan</span>`;
        }
      } else {
        row.classList.remove('shift-row-active');
        statusCell.innerHTML = `<span class="shift-status-pill standby">&#9898; Standby</span>`;
      }
    });

    // Update PLC status dot
    const plcDot = document.getElementById('plcStatusDot');
    const plcText = document.getElementById('plcStatusText');
    if (plcDot) plcDot.className = st.plcOnline ? 'status-dot dot-green' : 'status-dot dot-amber';
    if (plcText) plcText.innerText = st.plcOnline ? 'PLC: Online (Connected)' : 'PLC: Offline (Simulasi)';

  } catch (_) { /* silent */ }
}

// =========================================================================
// POLLING: CEK AKHIR SHIFT (<=5 MENIT SEBELUM PERGANTIAN)
// =========================================================================

let _lastShiftEndNotified = ''; // flag agar popup hanya muncul 1x per pergantian

async function startShiftEndPolling() {
  await checkShiftEnd(); // langsung cek pertama kali
  setInterval(checkShiftEnd, 30000); // setiap 30 detik
}

async function checkShiftEnd() {
  try {
    const res = await fetch('api/shift/end-check');
    if (!res.ok) return;
    const data = await res.json();

    if (data.isEndOfShift) {
      // Buat key unik per shift yang akan berakhir
      const key = `${data.currentShift}-${new Date().toDateString()}`;
      if (key !== _lastShiftEndNotified) {
        _lastShiftEndNotified = key;
        openShiftEndModal(data);
      }
    }

    // Update countdown di modal jika sedang terbuka
    const backdrop = document.getElementById('shiftEndModalBackdrop');
    if (backdrop && backdrop.classList.contains('show')) {
      updateModalCountdown(data.secondsLeft || data.minutesLeft * 60);
    }
  } catch (_) { /* silent */ }
}

// =========================================================================
// POPUP MODAL: AKHIR SHIFT
// =========================================================================

function setupShiftEndModal() {
  const btnOk = document.getElementById('btnModalSendD1000');
  const btnDismiss = document.getElementById('btnModalDismiss');
  const btnTest = document.getElementById('btnTestShiftEndPopup');

  // Tombol "OK - Kirim ke HMI (D1000 = 0)" DINONAKTIFKAN (sama dengan fitur HOME, endpoint backend sudah dihapus)

  if (btnDismiss) {
    btnDismiss.addEventListener('click', () => closeShiftEndModal());
  }

  if (btnTest) {
    btnTest.addEventListener('click', () => {
      openShiftEndModal({
        currentShift: 'Shift Test (Simulasi)',
        minutesLeft: 3,
        secondsLeft: 180,
        nextShift: 'Shift Berikutnya'
      });
    });
  }
}

function openShiftEndModal(data) {
  const backdrop = document.getElementById('shiftEndModalBackdrop');
  const infoEl = document.getElementById('modalShiftInfo');
  const btnOk = document.getElementById('btnModalSendD1000');

  if (infoEl) {
    infoEl.innerHTML = `
      <strong>Shift Aktif:</strong> ${data.currentShift || '-'}<br>
      <strong>Pergantian ke:</strong> ${data.nextShift || '-'}<br>
      <strong>Sisa Waktu:</strong> ${data.minutesLeft} menit
    `;
  }

  if (btnOk) {
    btnOk.disabled = true; // Fitur kirim D1000 dinonaktifkan
  }

  updateModalCountdown(data.secondsLeft || (data.minutesLeft || 5) * 60);

  if (backdrop) backdrop.classList.add('show');
  addLog(`[SHIFT END] Peringatan: ${data.currentShift} akan berakhir dalam ${data.minutesLeft} menit! Popup ditampilkan.`, 'info');
}

function closeShiftEndModal() {
  const backdrop = document.getElementById('shiftEndModalBackdrop');
  if (backdrop) backdrop.classList.remove('show');
}

let _countdownInterval = null;
function updateModalCountdown(totalSeconds) {
  const el = document.getElementById('modalCountdown');
  if (!el) return;
  if (_countdownInterval) clearInterval(_countdownInterval);

  let remaining = totalSeconds;
  function tick() {
    if (remaining <= 0) {
      el.innerText = '00:00';
      clearInterval(_countdownInterval);
      return;
    }
    const m = Math.floor(remaining / 60);
    const s = remaining % 60;
    el.innerText = `${m}:${String(s).padStart(2, '0')}`;
    remaining--;
  }
  tick();
  _countdownInterval = setInterval(tick, 1000);
}

/* =========================================================================
   PAGE VIEW SWITCHER & NAVIGATION
   ========================================================================= */
let currentActiveView = 'dispatcher';

function switchView(viewName) {
  currentActiveView = viewName;

  // Update Nav Buttons
  const navDispatcher = document.getElementById('navDispatcher');
  const navAp = document.getElementById('navApProduct');
  const navMachine = document.getElementById('navMasterMachine');
  const navMaster = document.getElementById('navMasterProduct');

  if (navDispatcher) navDispatcher.classList.toggle('active', viewName === 'dispatcher');
  if (navAp) navAp.classList.toggle('active', viewName === 'approduct');
  if (navMachine) navMachine.classList.toggle('active', viewName === 'mastermachine');
  if (navMaster) navMaster.classList.toggle('active', viewName === 'masterproduct');

  // Update Views
  const viewDispatcherEl = document.getElementById('viewDispatcher');
  const viewApEl = document.getElementById('viewApProduct');
  const viewMachineEl = document.getElementById('viewMasterMachine');
  const viewMasterEl = document.getElementById('viewMasterProduct');

  if (viewDispatcherEl) viewDispatcherEl.classList.toggle('active', viewName === 'dispatcher');
  if (viewApEl) viewApEl.classList.toggle('active', viewName === 'approduct');
  if (viewMachineEl) viewMachineEl.classList.toggle('active', viewName === 'mastermachine');
  if (viewMasterEl) viewMasterEl.classList.toggle('active', viewName === 'masterproduct');

  // Auto Load Data when opening view
  if (viewName === 'dispatcher') {
    loadMasterSutMap(); // SUT terbaru dari Master Data Produk
  } else if (viewName === 'approduct') {
    loadApProducts();
  } else if (viewName === 'mastermachine') {
    loadMasterData('machine');
  } else if (viewName === 'masterproduct') {
    loadMasterData('product');
  }
}

/* =========================================================================
   OFFICIAL MACHINE LIST MANAGEMENT (17 MESIN PANASONIC)
   ========================================================================= */
let cachedMachineList = [];

async function loadMachineList() {
  try {
    const res = await fetch('api/machines');
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const list = await res.json();
    if (Array.isArray(list) && list.length > 0) {
      cachedMachineList = list;
      populateMachineDropdowns(list);
      return;
    }
  } catch (e) {
    console.warn('Gagal memuat daftar mesin dari API, menggunakan fallback 17 mesin:', e);
  }

  // Fallback 17 Mesin Resmi Panasonic
  const defaults = [
    { idMachine: 1, machineName: '400 Ton New' },
    { idMachine: 2, machineName: '300 Ton New' },
    { idMachine: 3, machineName: '300 Ton Old' },
    { idMachine: 4, machineName: '200 Ton' },
    { idMachine: 5, machineName: '110 Ton Amada' },
    { idMachine: 6, machineName: '110 Ton Komatsu' },
    { idMachine: 7, machineName: 'Hairpin Bender 7row' },
    { idMachine: 8, machineName: 'Hairpin Bender 14row' },
    { idMachine: 9, machineName: 'Fix 80' },
    { idMachine: 10, machineName: 'Fix 36' },
    { idMachine: 11, machineName: 'Fix 12' },
    { idMachine: 12, machineName: 'SF-250' },
    { idMachine: 13, machineName: 'Expander Kyoshin 7' },
    { idMachine: 14, machineName: 'Expander Kyoshin 6.35' },
    { idMachine: 15, machineName: 'Expander Li-chin' },
    { idMachine: 1002, machineName: 'Evaporator' },
    { idMachine: 1003, machineName: 'Condenser' }
  ];
  cachedMachineList = defaults;
  populateMachineDropdowns(defaults);
}

function populateMachineDropdowns(machines) {
  const getOptHtml = (m) => {
    const name = m.machineName || m.MachineName || m;
    return `<option value="${escapeHtml(name)}">${escapeHtml(name)}</option>`;
  };

  // 1. apMachineFilter
  const apFilter = document.getElementById('apMachineFilter');
  if (apFilter) {
    const currentVal = apFilter.value;
    apFilter.innerHTML = '<option value="">Semua Mesin</option>' + machines.map(getOptHtml).join('');
    if (currentVal) apFilter.value = currentVal;
  }

  // 2. apFormMachine
  const apForm = document.getElementById('apFormMachine');
  if (apForm) {
    const currentVal = apForm.value;
    apForm.innerHTML = machines.map(getOptHtml).join('');
    if (currentVal) apForm.value = currentVal;
  }

  // 4. masterFormMachine
  const masterForm = document.getElementById('masterFormMachine');
  if (masterForm) {
    const currentVal = masterForm.value;
    masterForm.innerHTML = machines.map(getOptHtml).join('');
    if (currentVal) masterForm.value = currentVal;
  }

  // 4b. Master Data Machine: filter & form mesin
  const machMasterFilter = document.getElementById('machMasterMachineFilter');
  if (machMasterFilter) {
    const currentVal = machMasterFilter.value;
    machMasterFilter.innerHTML = '<option value="">Semua Mesin</option>' + machines.map(getOptHtml).join('');
    if (currentVal) machMasterFilter.value = currentVal;
  }

  const machMasterForm = document.getElementById('machMasterFormMachine');
  if (machMasterForm) {
    const currentVal = machMasterForm.value;
    machMasterForm.innerHTML = machines.map(getOptHtml).join('');
    if (currentVal) machMasterForm.value = currentVal;
  }

  // 5. machineSelectFilter & unitTypeFilter (Terkunci Khusus Mesin CU MCH1-01)
  const planMachineSelect = document.getElementById('machineSelectFilter');
  if (planMachineSelect) {
    planMachineSelect.innerHTML = '<option value="MCH1-01" selected>Expander Kyoshin 6.35</option>';
    planMachineSelect.value = 'MCH1-01';
  }

  const unitFilter = document.getElementById('unitTypeFilter');
  if (unitFilter) {
    unitFilter.innerHTML = '<option value="CU" selected>CU (Outdoor Unit) ★</option>';
    unitFilter.value = 'CU';
  }
}

function renderMachineBadge(machineName) {
  if (!machineName) return '<span class="badge-machine-cu">-</span>';
  const m = String(machineName).toLowerCase();
  if (m.includes('condenser') || m.includes('cu') || m.includes('400') || m.includes('200') || m.includes('fix') || m.includes('sf')) {
    return `<span class="badge-machine-cu">${escapeHtml(machineName)}</span>`;
  }
  return `<span class="badge-machine-cs">${escapeHtml(machineName)}</span>`;
}

/* =========================================================================
   DATA APPRODUCT MANAGEMENT (CRUD + LIVE SEARCH)
   ========================================================================= */
let cachedApProducts = [];
let apSearchDebounceTimer = null;

function onApSearchChanged() {
  if (apSearchDebounceTimer) clearTimeout(apSearchDebounceTimer);
  apSearchDebounceTimer = setTimeout(() => {
    loadApProducts();
  }, 250);
}

function resetApFilter() {
  const sInput = document.getElementById('apSearchInput');
  const mFilter = document.getElementById('apMachineFilter');
  const dFilter = document.getElementById('apDateFilter');

  if (sInput) sInput.value = '';
  if (mFilter) mFilter.value = '';
  if (dFilter) dFilter.value = '';

  loadApProducts();
}

async function loadApProducts() {
  const tbody = document.getElementById('apProductTableBody');
  const countBadge = document.getElementById('apRecordCountBadge');

  const sInput = document.getElementById('apSearchInput');
  const mFilter = document.getElementById('apMachineFilter');
  const dFilter = document.getElementById('apDateFilter');

  const searchVal = sInput ? sInput.value.trim() : '';
  const machineVal = mFilter ? mFilter.value : '';
  const dateVal = dFilter ? dFilter.value : '';

  let url = 'api/approduct?';
  const params = new URLSearchParams();
  if (searchVal) params.append('search', searchVal);
  if (machineVal) params.append('machine', machineVal);
  if (dateVal) params.append('date', dateVal);
  url += params.toString();

  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    cachedApProducts = data;

    if (countBadge) countBadge.innerText = `${data.length} Data Ditemukan`;

    if (!tbody) return;

    if (data.length === 0) {
      tbody.innerHTML = `
        <tr>
          <td colspan="9" style="text-align:center; padding: 28px; color: #94a3b8;">
            Tidak ada data APproduct yang cocok dengan filter pencarian.
          </td>
        </tr>
      `;
      return;
    }

    tbody.innerHTML = data.map((item, idx) => {
      const machineBadge = renderMachineBadge(item.machine);

      // Format datetime DD-MM-YYYY HH:mm:ss
      let dateStr = '-';
      if (item.date) {
        try {
          const d = new Date(item.date);
          if (!isNaN(d.getTime())) {
            const dd = String(d.getDate()).padStart(2, '0');
            const mm = String(d.getMonth() + 1).padStart(2, '0');
            const yyyy = d.getFullYear();
            const hh = String(d.getHours()).padStart(2, '0');
            const min = String(d.getMinutes()).padStart(2, '0');
            const ss = String(d.getSeconds()).padStart(2, '0');
            dateStr = `${dd}-${mm}-${yyyy} ${hh}:${min}:${ss}`;
          } else {
            dateStr = item.date.substring(0, 19).replace('T', ' ');
          }
        } catch (e) {
          dateStr = item.date;
        }
      }

      // Deteksi Shift & Status Overtime (Contoh: 'Shift 1' atau 'Overtime Shift 1')
      const detected = detectShiftAndOvertimeJs(item.date);
      
      const isOt = (item.isOvertime !== undefined && item.isOvertime !== null) ? Boolean(item.isOvertime) : detected.isOvertime;
      
      let rawShift = item.shift || detected.shiftName;
      // Ekstrak nama dasar shift (Shift 1, Shift 2, Shift 3, Non-Shift)
      let baseShift = rawShift.replace(/Overtime\s*/i, '').replace(/\s*\([^)]*\)/g, '').trim();
      if (!baseShift) baseShift = 'Shift 1';

      const displayLabel = isOt ? `Overtime ${baseShift}` : baseShift;

      let sClass = 's1';
      if (baseShift.toLowerCase().includes('shift 2') || baseShift.includes('2')) sClass = 's2';
      else if (baseShift.toLowerCase().includes('shift 3') || baseShift.includes('3')) sClass = 's3';
      else if (baseShift.toLowerCase().includes('ns') || baseShift.toLowerCase().includes('non')) sClass = 'ns';

      const badgeClass = isOt ? `ot-badge ot-${sClass}` : sClass;

      // camelCase dari C# JSON: totalAct, plan, prodPerDay
      const totalAct = item.totalAct ?? item.total_act ?? 0;
      const plan     = item.plan ?? 0;
      const prodPerDay = item.prodPerDay ?? item.prod_per_day ?? 0;

      return `
        <tr>
          <td style="text-align:center; font-weight:700; color:#64748b;">${idx + 1}</td>
          <td><span style="font-family:'JetBrains Mono',monospace; font-weight:600; font-size:12px; color:#1e293b;">${dateStr}</span></td>
          <td style="text-align:center;">
            <span class="shift-badge ${badgeClass}">${displayLabel}</span>
          </td>
          <td>${machineBadge}</td>
          <td><span class="model-code-tag">${escapeHtml(item.model)}</span></td>
          <td class="num-cell" style="color:#047857;">${Number(totalAct).toLocaleString()}</td>
          <td class="num-cell" style="color:#1d4ed8;">${Number(plan).toLocaleString()}</td>
          <td class="num-cell">${Number(prodPerDay).toLocaleString()}</td>
          <td style="text-align:center;">
            <div class="btn-action-group">
              <button class="btn-action-edit" onclick="openApModal(${item.id})" title="Edit Data">Edit</button>
              <button class="btn-action-delete" onclick="deleteApProduct(${item.id})" title="Hapus Data">Hapus</button>
            </div>
          </td>
        </tr>
      `;
    }).join('');

  } catch (err) {
    console.error('Error loading APproduct data:', err);
    if (tbody) {
      tbody.innerHTML = `
        <tr>
          <td colspan="9" style="text-align:center; padding: 24px; color: #dc2626;">
            Gagal mengambil data APproduct. Pastikan server aktif.
          </td>
        </tr>
      `;
    }
  }
}

/* =========================================================================
   HELPER: DETEKSI SHIFT & OVERTIME BERDASARKAN JADWAL PANASONIC
   ========================================================================= */
function detectShiftAndOvertimeJs(dateStr) {
  if (!dateStr) return { shiftName: 'Shift 1', shiftClass: 's1', isOvertime: false, displayLabel: 'Shift 1' };

  let d = new Date(dateStr);
  if (isNaN(d.getTime())) {
    const cleaned = String(dateStr).replace(' ', 'T');
    d = new Date(cleaned);
  }

  if (isNaN(d.getTime())) {
    return { shiftName: 'Shift 1', shiftClass: 's1', isOvertime: false, displayLabel: 'Shift 1' };
  }

  // Cek Hari Libur / Akhir Pekan (Sabtu = 6, Minggu = 0)
  const day = d.getDay();
  const isWeekend = (day === 0 || day === 6);

  const hours = d.getHours();
  const minutes = d.getMinutes();
  const timeMinutes = hours * 60 + minutes;

  let baseShift = 'Shift 1';
  let shiftClass = 's1';

  // Jadwal Shift Panasonic:
  // Shift 1: 07:00 - 15:45 (420 - 945 min)
  // Shift 2: 15:45 - 23:15 (945 - 1395 min)
  // Shift 3: 23:15 - 07:00 (1395 - 420 min)
  if (timeMinutes >= 420 && timeMinutes < 945) {
    baseShift = 'Shift 1';
    shiftClass = 's1';
  } else if (timeMinutes >= 945 && timeMinutes < 1395) {
    baseShift = 'Shift 2';
    shiftClass = 's2';
  } else {
    baseShift = 'Shift 3';
    shiftClass = 's3';
  }

  const displayLabel = isWeekend ? `Overtime ${baseShift}` : baseShift;

  return {
    shiftName: baseShift,
    shiftClass: shiftClass,
    isOvertime: isWeekend,
    displayLabel: displayLabel
  };
}

function onApModalDateChanged() {
  const dateInput = document.getElementById('apFormDate');
  const shiftBadge = document.getElementById('apModalShiftBadge');
  if (!dateInput || !shiftBadge) return;

  const dtVal = dateInput.value;
  const detected = detectShiftAndOvertimeJs(dtVal);

  const badgeClass = detected.isOvertime ? `ot-badge ot-${detected.shiftClass}` : detected.shiftClass;
  shiftBadge.className = `shift-badge ${badgeClass}`;
  shiftBadge.innerText = detected.displayLabel;
}

function openApModal(id = null) {
  const backdrop = document.getElementById('modalApProductBackdrop');
  const titleEl = document.getElementById('modalApTitle');
  const idInput = document.getElementById('apFormId');
  const dateInput = document.getElementById('apFormDate');
  const machineSelect = document.getElementById('apFormMachine');
  const modelInput = document.getElementById('apFormModel');
  const actInput = document.getElementById('apFormTotalAct');
  const planInput = document.getElementById('apFormPlan');
  const prodInput = document.getElementById('apFormProdPerDay');

  const saveAsNewGroup = document.getElementById('apSaveAsNewRowGroup');
  const saveAsNewCheck = document.getElementById('apSaveAsNewRow');

  if (id && id > 0) {
    const item = cachedApProducts.find(x => x.id === id);
    if (titleEl) titleEl.innerText = 'Edit Data APproduct';
    if (idInput) idInput.value = item ? item.id : id;
    if (saveAsNewGroup) saveAsNewGroup.style.display = 'block';
    if (saveAsNewCheck) saveAsNewCheck.checked = true; // Default: Catat baris baru bila ada perubahan

    // Isi datetime-local (format: YYYY-MM-DDTHH:mm)
    if (dateInput) {
      let dtVal = '';
      if (item && item.date) {
        try {
          const d = new Date(item.date);
          if (!isNaN(d.getTime())) {
            const yyyy = d.getFullYear();
            const mm = String(d.getMonth() + 1).padStart(2, '0');
            const dd = String(d.getDate()).padStart(2, '0');
            const hh = String(d.getHours()).padStart(2, '0');
            const min = String(d.getMinutes()).padStart(2, '0');
            dtVal = `${yyyy}-${mm}-${dd}T${hh}:${min}`;
          } else {
            dtVal = item.date.substring(0, 16).replace(' ', 'T');
          }
        } catch(e) { dtVal = ''; }
      }
      dateInput.value = dtVal;
    }
    if (machineSelect) machineSelect.value = item ? item.machine : 'Condenser';
    if (modelInput) modelInput.value = item ? item.model : '';
    if (actInput) actInput.value = item ? (item.totalAct ?? item.total_act ?? 0) : 0;
    if (planInput) planInput.value = item ? (item.plan ?? 0) : 0;
    if (prodInput) prodInput.value = item ? (item.prodPerDay ?? item.prod_per_day ?? 0) : 0;
  } else {
    if (titleEl) titleEl.innerText = 'Tambah Data APproduct';
    if (idInput) idInput.value = 0;
    if (saveAsNewGroup) saveAsNewGroup.style.display = 'none';

    if (dateInput) {
      // Default: sekarang (datetime-local format)
      const now = new Date();
      const yyyy = now.getFullYear();
      const mm = String(now.getMonth() + 1).padStart(2, '0');
      const dd = String(now.getDate()).padStart(2, '0');
      const hh = String(now.getHours()).padStart(2, '0');
      const min = String(now.getMinutes()).padStart(2, '0');
      dateInput.value = `${yyyy}-${mm}-${dd}T${hh}:${min}`;
    }
    if (machineSelect) machineSelect.value = 'Condenser';
    if (modelInput) modelInput.value = '';
    if (actInput) actInput.value = 0;
    if (planInput) planInput.value = 0;
    if (prodInput) prodInput.value = 0;
  }

  // Update live shift badge di modal
  onApModalDateChanged();

  if (backdrop) backdrop.classList.add('show');
}

function closeApModal() {
  const backdrop = document.getElementById('modalApProductBackdrop');
  if (backdrop) backdrop.classList.remove('show');
}

async function saveApProduct(e) {
  e.preventDefault();

  const id = parseInt(document.getElementById('apFormId').value, 10) || 0;
  const isNew = (id === 0);
  const saveAsNewCheck = document.getElementById('apSaveAsNewRow');
  const saveAsNew = isNew || (saveAsNewCheck && saveAsNewCheck.checked);

  const date = document.getElementById('apFormDate').value;
  const machine = document.getElementById('apFormMachine').value;
  const model = document.getElementById('apFormModel').value.trim();
  const total_act = parseInt(document.getElementById('apFormTotalAct').value, 10) || 0;
  const plan = parseInt(document.getElementById('apFormPlan').value, 10) || 0;
  const prod_per_day = parseInt(document.getElementById('apFormProdPerDay').value, 10) || 0;

  if (!model) {
    showAppModal('warning', 'Nama Model Kosong', 'Harap masukkan nama model sebelum menyimpan.');
    return;
  }

  const detected = detectShiftAndOvertimeJs(date);

  const targetId = saveAsNew ? 0 : id;
  const payload = {
    id: targetId,
    date: date,
    machine: machine,
    model: model,
    totalAct: total_act,
    plan: plan,
    prodPerDay: prod_per_day,
    shift: detected.shiftName,
    isOvertime: detected.isOvertime
  };

  const url = saveAsNew ? 'api/approduct' : `api/approduct/${id}`;
  const method = saveAsNew ? 'POST' : 'PUT';

  try {
    const res = await fetch(url, {
      method: method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      const errTxt = await res.text();
      throw new Error(errTxt || 'HTTP ' + res.status);
    }

    closeApModal();
    loadApProducts();
    const actionText = saveAsNew ? 'disimpan sebagai baris baru' : 'diperbarui';
    showAppModal('success', 'Data APproduct Disimpan', `Data model ${model} (${machine}) berhasil ${actionText}.`);
    addLog(`[APPRODUCT] Data model ${model} (${machine}) berhasil ${actionText}.`, 'success');
  } catch (err) {
    console.error('Error saving APproduct:', err);
    showAppModal('error', 'Gagal Menyimpan Data', `Terjadi kesalahan saat menyimpan data: ${err.message}`);
  }
}

async function deleteApProduct(id) {
  const item = cachedApProducts.find(x => x.id === id);
  const modelName = item ? item.model : `ID ${id}`;

  if (!confirm(`Apakah Anda yakin ingin menghapus data APproduct untuk model "${modelName}"?`)) {
    return;
  }

  try {
    const res = await fetch(`api/approduct/${id}`, { method: 'DELETE' });
    if (!res.ok) throw new Error('HTTP ' + res.status);

    loadApProducts();
    showAppModal('info', 'Data APproduct Dihapus', `Data model ${modelName} telah berhasil dihapus.`);
    addLog(`[APPRODUCT] Data model ${modelName} dihapus.`, 'info');
  } catch (err) {
    console.error('Error deleting APproduct:', err);
    showAppModal('error', 'Gagal Menghapus Data', `Terjadi kesalahan saat menghapus data: ${err.message}`);
  }
}

/* =========================================================================
   MASTER DATA MANAGEMENT (CRUD + LIVE SEARCH)
   Dipakai oleh 2 halaman dengan struktur sama & data terpisah:
   - 'machine' : Master Data Machine (/api/mastermachine)
   - 'product' : Master Data Produk  (/api/masterproduct)
   ========================================================================= */
const MASTER_PAGES = {
  product: {
    api: 'api/masterproduct',
    label: 'Master Data Produk',
    logTag: 'MASTER PRODUCT',
    ids: {
      search: 'masterSearchInput', machineSelect: 'masterMachineSelect', count: 'masterRecordCountBadge',
      tbody: 'masterProductTableBody', backdrop: 'modalMasterBackdrop', title: 'modalMasterTitle',
      formId: 'masterFormId', formModel: 'masterFormModel', formMachine: 'masterFormMachine',
      formSut: 'masterFormSut'
    },
    showOpQty: false, // Kolom No Of Operator & Qty / Hour tidak ditampilkan di Master Data Produk
    searchModelOnly: true, // Kotak cari hanya mencocokkan nama model (bukan mesin)
    cache: [],
    debounce: null
  },
  machine: {
    api: 'api/mastermachine',
    label: 'Master Data Machine',
    logTag: 'MASTER MACHINE',
    ids: {
      search: 'machMasterSearchInput', machineFilter: 'machMasterMachineFilter', count: 'machMasterRecordCountBadge',
      tbody: 'machMasterTableBody', backdrop: 'machMasterModalBackdrop', title: 'machMasterModalTitle',
      formId: 'machMasterFormId', formModel: 'machMasterFormModel', formMachine: 'machMasterFormMachine',
      formSut: 'machMasterFormSut', formOp: 'machMasterFormNoOfOperator', formQty: 'machMasterFormQtyPerHour'
    },
    showOpQty: true,
    cache: [],
    debounce: null
  }
};

const masterEl = (key, name) => {
  const id = MASTER_PAGES[key].ids[name];
  return id ? document.getElementById(id) : null;
};

// Ambil pesan error dari server ({ success:false, message }) agar operator tahu penyebabnya
async function masterApiError(res) {
  let msg = 'HTTP ' + res.status;
  try {
    const body = await res.json();
    if (body && body.message) msg = body.message;
  } catch (e) { /* respons bukan JSON */ }
  return new Error(msg);
}

function onMasterDataSearchChanged(key) {
  const page = MASTER_PAGES[key];
  if (page.debounce) clearTimeout(page.debounce);
  page.debounce = setTimeout(() => loadMasterData(key), 250);
}

function resetMasterDataFilter(key) {
  const sInput = masterEl(key, 'search');
  const mFilter = masterEl(key, 'machineFilter');
  const mSelect = masterEl(key, 'machineSelect');
  if (sInput) sInput.value = '';
  if (mFilter) mFilter.value = '';
  if (mSelect) mSelect.value = '';
  loadMasterData(key);
}

async function loadMasterData(key) {
  const page = MASTER_PAGES[key];
  const tbody = masterEl(key, 'tbody');
  const countBadge = masterEl(key, 'count');
  const sInput = masterEl(key, 'search');
  const mFilter = masterEl(key, 'machineFilter');

  const searchVal = sInput ? sInput.value.trim() : '';
  const machineVal = mFilter ? mFilter.value : '';

  const params = new URLSearchParams();
  if (searchVal && !page.searchModelOnly) params.append('search', searchVal);
  if (machineVal) params.append('machine', machineVal);
  const url = `${page.api}?${params.toString()}`;

  try {
    const res = await fetch(url);
    if (!res.ok) throw await masterApiError(res);
    let data = await res.json();
    const fullData = data; // daftar pilihan Machine diambil dari seluruh data, bukan hasil pencarian

    if (searchVal && page.searchModelOnly) {
      const s = searchVal.toUpperCase();
      data = data.filter(x => (x.model || '').toUpperCase().includes(s));
    }

    // Filter Machine (Master Data Produk): pilihan = "All Machine" + nama mesin yang ada di data
    const mSelect = masterEl(key, 'machineSelect');
    if (mSelect) {
      const current = mSelect.value;
      const machines = [...new Set(fullData.map(x => (x.machine || '').trim()).filter(Boolean))].sort();
      mSelect.innerHTML = '<option value="">All Machine</option>' +
        machines.map(m => `<option value="${escapeHtml(m)}">${escapeHtml(m)}</option>`).join('');
      mSelect.value = machines.includes(current) ? current : '';
      if (mSelect.value) {
        data = data.filter(x => (x.machine || '').trim() === mSelect.value);
      }
    }
    page.cache = data;

    if (countBadge) countBadge.innerText = `${data.length} Data Ditemukan`;

    if (!tbody) return;

    if (data.length === 0) {
      tbody.innerHTML = `
        <tr>
          <td colspan="${page.showOpQty ? 7 : 5}" style="text-align:center; padding: 28px; color: #94a3b8;">
            Tidak ada data ${page.label} yang cocok dengan filter pencarian.
          </td>
        </tr>
      `;
      return;
    }

    tbody.innerHTML = data.map((item, idx) => {
      const machineBadge = renderMachineBadge(item.machine);

      const opCount = item.noOfOperator ?? item.noofoperator ?? item.no_of_operator ?? 12;
      const qtyH = item.qtyPerHour ?? item.qty_per_hour ?? item.qtyperhour ?? 0;

      return `
        <tr>
          <td style="text-align:center; font-weight:700; color:#64748b;">${idx + 1}</td>
          <td><span class="model-code-tag">${escapeHtml(item.model)}</span></td>
          <td>${machineBadge}</td>
          <td class="num-cell" style="color:#d97706;">${Number(Number(item.sut).toFixed(2))} s</td>
          ${page.showOpQty ? `<td class="num-cell">${opCount} Org</td>` : ''}
          ${page.showOpQty ? `<td class="num-cell" style="color:#047857; font-weight:800;">${Number(qtyH).toFixed(1)}</td>` : ''}
          <td style="text-align:center;">
            <div class="btn-action-group">
              <button class="btn-action-edit" onclick="openMasterDataModal('${key}', ${item.id})" title="Edit ${page.label}">Edit</button>
              <button class="btn-action-delete" onclick="deleteMasterData('${key}', ${item.id})" title="Hapus ${page.label}">Hapus</button>
            </div>
          </td>
        </tr>
      `;
    }).join('');

  } catch (err) {
    console.error(`Error loading ${page.label}:`, err);
    if (tbody) {
      tbody.innerHTML = `
        <tr>
          <td colspan="${page.showOpQty ? 7 : 5}" style="text-align:center; padding: 24px; color: #dc2626;">
            Gagal mengambil ${page.label}: ${escapeHtml(err.message)}
          </td>
        </tr>
      `;
    }
  }
}

// Pastikan dropdown mesin memuat semua nama mesin yang sudah dipakai di data (mis. "Expander 635"),
// agar nilai lama tidak berubah sendiri saat Edit -> Simpan.
function ensureMachineOptions(selectEl, names) {
  if (!selectEl) return;
  const existing = new Set([...selectEl.options].map(o => o.value));
  names.forEach(name => {
    if (name && !existing.has(name)) {
      selectEl.add(new Option(name, name));
      existing.add(name);
    }
  });
}

function openMasterDataModal(key, id = null) {
  const page = MASTER_PAGES[key];
  ensureMachineOptions(masterEl(key, 'formMachine'), page.cache.map(x => x.machine));
  const backdrop = masterEl(key, 'backdrop');
  const titleEl = masterEl(key, 'title');
  const idInput = masterEl(key, 'formId');
  const modelInput = masterEl(key, 'formModel');
  const machineSelect = masterEl(key, 'formMachine');
  const sutInput = masterEl(key, 'formSut');
  const opInput = masterEl(key, 'formOp');
  const qtyInput = masterEl(key, 'formQty');

  if (id && id > 0) {
    const item = page.cache.find(x => x.id === id);
    if (titleEl) titleEl.innerText = `Edit ${page.label}`;
    if (idInput) idInput.value = item ? item.id : id;
    if (modelInput) modelInput.value = item ? item.model : '';
    if (machineSelect) machineSelect.value = item ? item.machine : 'Condenser';
    if (sutInput) sutInput.value = item ? item.sut : 23.0;
    if (opInput) opInput.value = item ? (item.noOfOperator ?? item.noofoperator ?? item.no_of_operator ?? 14) : 14;
    if (qtyInput) qtyInput.value = item ? (item.qtyPerHour ?? item.qty_per_hour ?? item.qtyperhour ?? 156.0) : 156.0;
  } else {
    if (titleEl) titleEl.innerText = `Tambah ${page.label}`;
    if (idInput) idInput.value = 0;
    if (modelInput) modelInput.value = '';
    if (machineSelect) machineSelect.value = page.cache[0]?.machine || 'Condenser'; // default: mesin yang sudah dipakai di data
    if (sutInput) sutInput.value = 23.0;
    if (opInput) opInput.value = 14;
    if (qtyInput) qtyInput.value = 156.0;
  }

  if (backdrop) backdrop.classList.add('show');
}

function closeMasterDataModal(key) {
  const backdrop = masterEl(key, 'backdrop');
  if (backdrop) backdrop.classList.remove('show');
}

async function saveMasterData(key, e) {
  e.preventDefault();
  const page = MASTER_PAGES[key];

  const id = parseInt(masterEl(key, 'formId')?.value || '0', 10) || 0;
  const model = masterEl(key, 'formModel')?.value.trim() || '';
  const machine = masterEl(key, 'formMachine')?.value || 'Condenser';
  const sut = parseFloat(masterEl(key, 'formSut')?.value || '0') || 0;
  // Halaman tanpa isian Operator / Qty (Master Data Produk): pertahankan nilai lama agar tidak tertimpa 0
  const existing = page.cache.find(x => x.id === id);
  const opEl = masterEl(key, 'formOp');
  const qtyEl = masterEl(key, 'formQty');
  const noofoperator = opEl
    ? (parseInt(opEl.value || '0', 10) || 0)
    : (existing?.noOfOperator ?? 0);
  const qty_per_hour = qtyEl
    ? (parseFloat(qtyEl.value || '0') || 0)
    : (existing?.qtyPerHour ?? 0);

  if (!model) {
    showAppModal('warning', 'Nama Model Kosong', 'Harap masukkan nama model sebelum menyimpan.');
    return;
  }

  const payload = {
    id: id,
    model: model,
    machine: machine,
    sut: sut,
    noOfOperator: noofoperator,
    noofoperator: noofoperator,
    qtyPerHour: qty_per_hour,
    qty_per_hour: qty_per_hour
  };

  const isNew = (id === 0);
  const url = isNew ? page.api : `${page.api}/${id}`;
  const method = isNew ? 'POST' : 'PUT';

  try {
    const res = await fetch(url, {
      method: method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      throw await masterApiError(res);
    }

    closeMasterDataModal(key);
    loadMasterData(key);
    showAppModal('success', `${page.label} Disimpan`, `Model ${model} berhasil ${isNew ? 'ditambahkan' : 'diperbarui'} di ${page.label}.`);
    addLog(`[${page.logTag}] Model ${model} (${machine}) berhasil disimpan.`, 'success');
  } catch (err) {
    console.error(`Error saving ${page.label}:`, err);
    showAppModal('error', `Gagal Menyimpan ${page.label}`, `Terjadi kesalahan saat menyimpan data: ${err.message}`);
  }
}

async function deleteMasterData(key, id) {
  const page = MASTER_PAGES[key];
  const item = page.cache.find(x => x.id === id);
  const modelName = item ? item.model : `ID ${id}`;

  if (!confirm(`Apakah Anda yakin ingin menghapus data model "${modelName}" dari ${page.label}?`)) {
    return;
  }

  try {
    const res = await fetch(`${page.api}/${id}`, { method: 'DELETE' });
    if (!res.ok) throw await masterApiError(res);

    loadMasterData(key);
    showAppModal('info', `${page.label} Dihapus`, `Data model ${modelName} telah berhasil dihapus dari ${page.label}.`);
    addLog(`[${page.logTag}] Data model ${modelName} dihapus.`, 'info');
  } catch (err) {
    console.error(`Error deleting ${page.label}:`, err);
    showAppModal('error', `Gagal Menghapus ${page.label}`, `Terjadi kesalahan saat menghapus data: ${err.message}`);
  }
}

// Nama fungsi lama untuk halaman Master Data Produk (dipanggil dari index.html)
const onMasterSearchChanged = () => onMasterDataSearchChanged('product');
const resetMasterFilter = () => resetMasterDataFilter('product');
const loadMasterProducts = () => loadMasterData('product');
const openMasterModal = (id = null) => openMasterDataModal('product', id);
const closeMasterModal = () => closeMasterDataModal('product');
const saveMasterProduct = (e) => saveMasterData('product', e);
const deleteMasterProduct = (id) => deleteMasterData('product', id);




