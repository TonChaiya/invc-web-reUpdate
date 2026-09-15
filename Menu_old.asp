<style type="text/css">
<!--
.style1 {color: #333}
-->
</style>


<style type="text/css">
*{
    padding: 0;
    margin: 10;
    text-decoration: none;
    list-style: none;
}
body {
    font: 12px/18px tahoma;
}
ul[id^="thaicss-nav"]:after {
    content: " ";
    display: block;
    clear: both;
}
ul[id$="horizontal"]>li {
    float: left;
    width: 150px;
    position: relative;
    background: #00FF66;
    border-left: solid 1px #ccc;
}
ul[id$="horizontal"]>li:first-child {
    border-left: none;
}
ul[id|="thaicss-nav"]>li>a, ul[id|="thaicss-nav"]>li:hover>ul>li>a {
    display: block;
    line-height: 20px;
}
ul[id|="thaicss-nav"]>li>a {
    text-align: center;
    color: #0000CC;
}
ul[id|="thaicss-nav"]>li:hover>ul>li>a {
    color: #FFF;
}
ul[id|="thaicss-nav"]>li:hover>a, ul[id|="thaicss-nav"]>li:hover>ul>li:hover>ul>li>a {
    color: #CCC;
}
ul[id|="thaicss-nav"]>li:hover, ul[id|="thaicss-nav"]>li>ul, ul[id|="thaicss-nav"]>li:hover>ul>li>ul {
    background: #666;
}
ul[id|="thaicss-nav"]>li:hover>ul>li:hover, ul[id|="thaicss-nav"]>li:hover>ul>li:hover>ul>li:hover {
    background: #444;
}
ul[id$="horizontal"]>li>ul, ul[id$="horizontal"]>li:hover>ul>li>ul {
    visibility: hidden;
    position: absolute;
    width: 150px;
}
ul[id|="thaicss-nav"]>li:hover>ul, ul[id|="thaicss-nav"]>li:hover>ul>li:hover>ul {
    visibility: visible;
}
ul[id|="thaicss-nav"]>li:hover>ul>li, ul[id|="thaicss-nav"]>li:hover>ul>li:hover>ul>li {
    line-height: 18px;
    padding-left: 3px;
    border-bottom: solid 1px #555;
    border-top: solid 1px #999;
}
ul[id$="horizontal"]>li:hover>ul>li:hover>ul {
    left: 150px;
    margin-top: -20px;
}
</style>

<ul id="thaicss-nav-horizontal">
    <li><a href="default.asp">Home</a></li>
    <li><a href="#">รายงาน</a>
 
        <ul>
            <li><a href="INV_Status.asp">รายงานมูลค่าคงคลังและการบริหารงบจัดซื้อ</a></li>
			<li><a href="PO_search.asp">ค้นหาใบสั่งซื้อ</a></li>
        </ul>
    </li>
    <li><a href="pending.asp">ตรวจสอบรายการยาค้างจ่าย</a></li>

    <li><a href="Findgen.asp">Abbr code checking</a></li>
<li><a href="ShelfList.asp">รายการยาตามชั้นเก็บ</a></li>
</ul>
