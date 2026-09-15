<style>
.navbar {
    min-height: 50px;
    background: #ffffff;
    border-bottom: 1px solid #e6e6e6;
    white-space: nowrap;
}

/* จัดวาง 3 โซนใน 1 แถว */
.navbar-flex {
    display: flex;
    justify-content: space-between;
    align-items: center;
    width: 100%;
}

/* กลางให้เรียงในแถวเดียว */
.navbar-center {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: 6px;
    flex: 1;
    white-space: nowrap;
}

/* dropdown ไม่ให้ตกบรรทัด */
.navbar-right {
    white-space: nowrap;
}

.navbar-form {
    margin: 0;
    padding: 0;
}

/* ปุ่ม modern */
.navbar-form .btn {
    border-radius: 6px;
    padding: 6px 12px;
    font-size: 13px;
    border: none;
}

.navbar-form .btn:hover {
    opacity: 0.85;
    transform: translateY(-1px);
    transition: 0.15s ease;
}

/* ช่องค้นหา */
.navbar-center .form-control {
    width: 240px;
    border-radius: 6px;
}

.navbar-nav > li > a {
    padding: 12px 10px;
    font-size: 13px;
}
</style>

<nav class="navbar navbar-default">
  <div class="container-fluid">

    <div class="navbar-flex">

      <!-- ?? ซ้ายสุด -->
      <a class="navbar-brand" href="default.asp">INVC</a>

      <!-- ?? กลางทั้งหมดอยู่แถวเดียว -->
      <form class="navbar-form navbar-center" method="get" action="INV_Status.asp">

        <a class="btn btn-default" href="default.asp">HOME</a>
        <a class="btn btn-primary" href="INV_Status.asp">สถานะคงคลัง</a>
        <a class="btn btn-success" href="PO_search.asp">ค้นหาใบสั่งซื้อ</a>
        <a class="btn btn-info" href="pending.asp">รายการค้างจ่าย</a>
        <a class="btn btn-warning" href="ShelfList.asp">รายการตามชั้นเก็บ</a>
        <a class="btn btn-danger" href="INV_Report_Purchase.asp">รายงานแนะนำซื้อ</a>


        <input name="SearchMenu" id="SearchMenu" class="form-control" placeholder="รหัสยา / ชื่อสามัญ / ชื่อการค้า">
        <button type="submit" class="btn btn-primary">ค้นหา</button>

      </form>

      <!-- ?? ขวาสุด dropdown -->
      <ul class="nav navbar-nav navbar-right">
        <li class="dropdown">
          <a href="#" class="dropdown-toggle" data-toggle="dropdown">
            INVC Links <span class="caret"></span>
          </a>
          <ul class="dropdown-menu">
            <li><a href="http://164.115.40.125/inv-c">INVC website</a></li>
            <li><a href="http://164.115.40.125/dmsic_www/main.php?mod=board&act=show_board_category">INVC Webboard</a></li>
            <li><a href="http://164.115.40.125/dmsic_www/main.php?mod=content&act=show_content_list_all">บทความ INVC</a></li>
            <li role="separator" class="divider"></li>
            <li><a href="https://dmsic.moph.go.th">DMSIC</a></li>
          </ul>
        </li>
      </ul>

    </div>
  </div>
</nav>
