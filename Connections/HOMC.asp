<!--#include file="INVFlood.asp" -->
<%
' สร้าง alias สำหรับโค้ดเก่าที่อาจอ้าง MM_HOMC_STRING / MM_HOMC_Conn
On Error Resume Next
If IsEmpty(MM_HOMC_STRING) Then MM_HOMC_STRING = MM_INVFlood_STRING
If IsObject(Conn) Then Set MM_HOMC_Conn = Conn
%>
