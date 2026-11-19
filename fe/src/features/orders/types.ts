import type { OrderStatus, OrderMaterialStatus } from '@/constants/orderStatus';
import type { AuditEntry } from '@/types/attachment';

export type Customer = {
  customerId: number;
  customerName: string;
  phone: string | null;
  email: string | null;
  defaultAddress: string | null;
  status: CustomerStatus;
  discontinueReason: string | null;
};
export type CustomerStatus = 'Active' | 'Discontinued';
export type CustomerQuery = {
  keyword?: string;
  status?: CustomerStatus;
  page?: number;
  pageSize?: number;
};
/** Khách hàng rút gọn cho ô chọn: đủ để lọc khách đang hợp tác. */
export type CustomerOption = { customerId: number; customerName: string; status: CustomerStatus };
export type SaveCustomerPayload = {
  customerName: string;
  phone?: string | null;
  email?: string | null;
  defaultAddress?: string | null;
};

export type ProductStatus = 'Active' | 'Discontinued';
export type ProductListItem = {
  productId: number;
  productCode: string;
  productName: string;
  sampleImageUrl: string | null;
  /** Khóa các ảnh phụ theo thứ tự hiển thị (tối đa 5). */
  extraImages: string[];
  status: ProductStatus;
  isUsed: boolean;
  createdAt: string;
};
export type ProductDetail = ProductListItem & {
  description: string | null;
  /** Màu đã có biến thể (do đơn hàng tạo). */
  colors: string[];
  history: AuditEntry[];
};
export type SaveProductPayload = {
  productCode: string;
  productName: string;
  sampleImageUrl?: string | null;
  /** Ảnh phụ theo thứ tự hiển thị (tối đa 5); bỏ trống = giữ nguyên, mảng rỗng = xóa hết. */
  extraImages?: string[];
  description?: string | null;
};
/** Định mức theo mã hàng (UC10): thành phần, biến thể màu × size và NVL của từng biến thể. */
export type ProductComponentItem = {
  productComponentId: number;
  componentName: string;
  componentType: 'Panel' | 'Trim';
  piecesPerGarment: number;
  status: string;
};
/** Màu – size do đơn hàng tạo ra (chỉ đọc). */
export type ProductVariantItem = {
  productVariantId: number;
  color: string;
  sizeCode: string;
  status: string;
};
/** Định mức theo màu áo, dùng chung mọi size (DEV-ALL-87). */
export type ProductNormItem = {
  productComponentMaterialId: number;
  componentName: string;
  color: string;
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  specNote: string | null;
  /** FABRIC / TRIM. */
  materialType: string;
};
/** Chi tiết cắt (Panel) thuộc một nhóm bìa; bìa cụ thể do đơn hàng khai (DEV-ALL-85). */
export type ProductMarkerComponentItem = {
  markerNo: number;
  componentName: string;
};
/** Vải của một bìa theo màu áo. */
export type ProductMarkerFabricItem = {
  markerNo: number;
  color: string;
  materialId: number;
  materialCode: string;
  materialName: string;
  materialColor: string;
};
export type ProductSpec = {
  productId: number;
  components: ProductComponentItem[];
  variants: ProductVariantItem[];
  norms: ProductNormItem[];
  markerComponents: ProductMarkerComponentItem[];
  markerFabrics: ProductMarkerFabricItem[];
};
export type SaveProductSpecPayload = {
  components: {
    productComponentId: number | null;
    componentName: string;
    componentType: 'Panel' | 'Trim';
    piecesPerGarment: number;
  }[];
  variants: {
    productVariantId: number | null;
    color: string;
    sizeCode: string;
  }[];
  norms: {
    componentName: string;
    color: string;
    materialId: number;
    specNote: string | null;
  }[];
  markerComponents: ProductMarkerComponentItem[];
  /** Màu áo dùng nhóm bìa (DEV-ALL-89); vải của nhóm do máy chủ lấy từ định mức. */
  markerColors: { markerNo: number; color: string }[];
};
export type ProductQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  status?: ProductStatus;
};

/** Dòng danh sách đơn cho Quản đốc: không có thành tiền (GBR-02). */
export type OrderOverviewListItem = {
  orderId: number;
  orderCode: string;
  customerName: string;
  receivedDate: string;
  dueDate: string;
  status: OrderStatus;
  materialStatus: OrderMaterialStatus;
  isDelayRisk: boolean;
  totalQty: number;
  productCodes: string;
};

export type OrderListItem = OrderOverviewListItem & { totalAmount: number };

export type OrderOverviewLine = {
  orderLineId: number;
  productId: number;
  productCode: string;
  productName: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
  /** Số đã giao (tổng số xuất của phiếu xuất đã giao). */
  deliveredQty?: number;
};

export type OrderLine = OrderOverviewLine & { cmtUnitPrice: number };

/** Dòng đơn để hiển thị: chỉ Admin có đơn giá. */
export type OrderDisplayLine = OrderOverviewLine & { cmtUnitPrice?: number };

/** Chi tiết đơn cho Quản đốc: không có đơn giá, lịch sử (GBR-02). */
export type OrderOverview = {
  orderId: number;
  orderCode: string;
  customerName: string;
  receivedDate: string;
  dueDate: string;
  status: OrderStatus;
  materialStatus: OrderMaterialStatus;
  isDelayRisk: boolean;
  stopReason: string | null;
  note: string | null;
  createdAt: string;
  lines: OrderOverviewLine[];
};

export type OrderDetail = {
  orderId: number;
  orderCode: string;
  customerId: number;
  customerName: string;
  receivedDate: string;
  dueDate: string;
  status: OrderStatus;
  materialStatus: OrderMaterialStatus;
  isDelayRisk: boolean;
  stopReason: string | null;
  note: string | null;
  createdAt: string;
  updatedAt: string | null;
  /** byte[] → base64; gửi nguyên chuỗi khi sửa / hủy / dừng. */
  rowVer: string;
  lines: OrderLine[];
  history: AuditEntry[];
};

export type OrderLineInput = {
  orderLineId: number | null;
  productId: number;
  color: string;
  sizeCode: string;
  orderedQty: number;
  cmtUnitPrice: number;
};

export type CreateOrderPayload = {
  orderCode: string;
  customerId: number;
  receivedDate: string;
  dueDate: string;
  note?: string | null;
  lines: OrderLineInput[];
};
export type UpdateOrderPayload = CreateOrderPayload & { rowVer: string };

export type OrderOverviewQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  status?: OrderStatus;
};

export type OrderQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  status?: OrderStatus;
  customerId?: number;
  dueFrom?: string;
  dueTo?: string;
};
/** Bìa cắt của đơn (DEV-ALL-85): số sản phẩm size này trên một lớp vải. */
export type MarkerRatio = { sizeCode: string; piecesPerPly: number };
export type OrderMarkerStatus = 'Active' | 'Discontinued';
export type OrderMarker = {
  productMarkerId: number;
  markerNo: number;
  markerCode: string;
  lengthM: number;
  /** Khổ bìa (m), tối đa 3,5 m (DEV-ALL-102). */
  widthM: number;
  status: OrderMarkerStatus;
  ratios: MarkerRatio[];
};
/** Nhóm bìa đã khai ở mã hàng và các màu của đơn dùng nhóm đó (DEV-ALL-89): bìa của nhóm dùng cho đúng các màu này. */
/** Vải của nhóm bìa ở các màu của đơn; widthM = khổ vải (m), null khi chưa nhập (DEV-ALL-104). */
export type MarkerGroupFabric = {
  materialId: number;
  materialCode: string;
  materialName: string;
  color: string;
  widthM: number | null;
};
export type OrderMarkerGroup = { markerNo: number; colors: string[]; fabrics: MarkerGroupFabric[] };
/** Một mã hàng của đơn: size có trong đơn, nhóm bìa đã khai ở mã hàng (kèm màu dùng nhóm), và các bìa của đơn. */
export type OrderMarkerProduct = {
  productId: number;
  productCode: string;
  productName: string;
  sizes: string[];
  markerGroups: OrderMarkerGroup[];
  markers: OrderMarker[];
};
export type OrderMarkers = { orderId: number; products: OrderMarkerProduct[] };
/** Gửi đủ bìa của một mã hàng trong đơn; bìa không có trong danh sách bị xóa. */
export type SaveOrderMarkersPayload = {
  markers: (Omit<OrderMarker, 'productMarkerId'> & { productMarkerId: number | null })[];
};

// ---- DEV-ALL-115: nhập Excel định mức mã hàng + bìa của đơn ----
/** Pending (DEV-ALL-122, chỉ ở popup kiểm tra Excel): đúng định dạng, cần màu / đơn chưa có nên kiểm khi Lưu đơn. */
export type SpecImportAction = 'New' | 'Update' | 'Unchanged' | 'Error' | 'Pending' | 'NewMaterial';
type SpecImportRowBase = { rowNumber: number; action: SpecImportAction; error: string | null };
export type SpecImportComponentRow = SpecImportRowBase & {
  componentName: string;
  /** PANEL | TRIM (mã như trong file), null khi ô sai. */
  componentType: string | null;
  piecesPerGarment: number | null;
};
export type SpecImportNormRow = SpecImportRowBase & {
  color: string;
  componentName: string;
  materialCode: string;
  materialName: string | null;
  unit: string | null;
  materialColor: string | null;
  specNote: string | null;
};
export type SpecImportMarkerGroupRow = SpecImportRowBase & {
  markerNo: number | null;
  componentNames: string[];
  colors: string[];
};
export type SpecImportOrderMarkerRow = SpecImportRowBase & {
  markerNo: number | null;
  markerCode: string;
  lengthM: number | null;
  widthM: number | null;
  status: string;
  ratios: MarkerRatio[];
};
export type SpecImportPreview = {
  productId: number;
  productCode: string;
  orderId: number | null;
  orderCode: string | null;
  components: SpecImportComponentRow[];
  norms: SpecImportNormRow[];
  markerGroups: SpecImportMarkerGroupRow[];
  orderMarkers: SpecImportOrderMarkerRow[];
  hasComponents: boolean;
  hasNorms: boolean;
  hasMarkerGroups: boolean;
  hasOrderMarkers: boolean;
  validCount: number;
  errorCount: number;
  duplicateCount: number;
  changeCount: number;
  /** Lỗi của bản đã gộp (kiểm tra của nút Lưu): còn lỗi thì không nhập được. */
  crossErrors: string[];
  notes: string[];
  canImport: boolean;
};
export type SpecImportResult = {
  components: number;
  norms: number;
  markerGroups: number;
  orderMarkers: number;
};
/** Màn Mã hàng (Admin) chọn đơn tùy ý; màn Đơn hàng (Admin, Quản đốc) chọn mã hàng của đơn. */
/** DEV-ALL-121: bảng của file định mức để nhập riêng (không truyền = mọi bảng trong file). */
export type SpecImportPart = 'components' | 'norms' | 'groups' | 'markers';
export type SpecImportTarget =
  | { scope: 'admin'; productId: number; orderId?: number; parts?: SpecImportPart[] }
  | { scope: 'manager'; orderId: number; productId: number };

// ---- DEV-ALL-118: nhập dòng hàng của đơn từ Excel (ma trận màu × size) ----
export type OrderImportQty = { sizeCode: string; qty: number };
export type OrderImportRow = {
  rowNumber: number;
  productCode: string;
  /** null khi dòng lỗi. */
  productId: number | null;
  productName: string | null;
  color: string;
  cmtUnitPrice: number | null;
  quantities: OrderImportQty[];
  error: string | null;
};
export type OrderImportPreview = {
  sizeColumns: string[];
  rows: OrderImportRow[];
  validCount: number;
  errorCount: number;
  duplicateCount: number;
};

// ---- DEV-ALL-120: tạo mã hàng hàng loạt từ Excel ----
export type ProductImportRow = {
  rowNumber: number;
  /** New | Exists (đã có, bỏ qua) | Error. */
  action: 'New' | 'Exists' | 'Error';
  note: string | null;
  productCode: string;
  productName: string;
  description: string | null;
};
export type ProductImportPreview = {
  rows: ProductImportRow[];
  newCount: number;
  existsCount: number;
  errorCount: number;
  duplicateCount: number;
};

// ---- DEV-ALL-122: popup kiểm tra file Excel nhiều sheet (dòng thô đọc từ file, sửa được trên màn hình) ----
/** parseError: lỗi định dạng ô khi đọc file; sửa ô trên popup thì xóa (máy chủ kiểm lại nội dung). */
type ExcelRowBase = { rowNumber: number; parseError: string | null };
export type ProductExcelRow = Omit<ExcelRowBase, 'parseError'> & {
  productCode: string;
  productName: string;
  description: string | null;
};
export type SpecComponentExcelRow = ExcelRowBase & {
  name: string;
  /** PANEL | TRIM. */
  componentType: string | null;
  piecesPerGarment: number | null;
};
export type SpecNormExcelRow = ExcelRowBase & {
  color: string;
  componentName: string;
  materialCode: string;
  specNote: string | null;
};
export type SpecMarkerGroupExcelRow = ExcelRowBase & {
  markerNo: number | null;
  componentNames: string[];
  colors: string[];
};
export type SpecOrderMarkerExcelRow = ExcelRowBase & {
  productCode: string | null;
  markerNo: number | null;
  markerCode: string;
  lengthM: number | null;
  widthM: number | null;
  /** ACTIVE | DISCONTINUED. */
  status: string;
  ratios: MarkerRatio[];
};
export type SpecWorkbook = {
  hasComponents: boolean;
  components: SpecComponentExcelRow[];
  hasNorms: boolean;
  norms: SpecNormExcelRow[];
  hasMarkerGroups: boolean;
  markerGroups: SpecMarkerGroupExcelRow[];
  hasOrderMarkers: boolean;
  orderMarkers: SpecOrderMarkerExcelRow[];
  sizeColumns: string[];
};
export type OrderMaterialExcelRow = ExcelRowBase & {
  materialCode: string;
  requiredQty: number | null;
  customerQty: number | null;
  note: string | null;
  /** DEV-ALL-123: cột tùy chọn Tên NVL / Đơn vị của file — gợi ý khi thêm NVL chưa có vào danh mục. */
  materialName?: string | null;
  unit?: string | null;
};
export type ExcelBundle = {
  hasProducts: boolean;
  products: ProductExcelRow[];
  spec: SpecWorkbook;
  hasOrderMaterials: boolean;
  orderMaterials: OrderMaterialExcelRow[];
};
/** Kết quả kiểm / xem trước NVL của đơn (khớp OrderMaterialImportPreviewDto của BE). */
export type OrderMaterialCheckRow = {
  rowNumber: number;
  /** New | Update | Unchanged | Error | Pending | NewMaterial. */
  action: SpecImportAction;
  error: string | null;
  materialCode: string;
  materialName: string | null;
  materialColor: string | null;
  unit: string | null;
  requiredQty: number | null;
  customerQty: number | null;
  purchaseQty: number | null;
  note: string | null;
};
export type OrderMaterialCheck = {
  rows: OrderMaterialCheckRow[];
  validCount: number;
  errorCount: number;
  duplicateCount: number;
  changeCount: number;
};
export type ExcelBundleCheck = {
  products: ProductImportPreview | null;
  spec: SpecImportPreview;
  orderMaterials: OrderMaterialCheck | null;
  /** DEV-ALL-123: mã NVL trong file chưa có trong danh mục (gợi ý loại theo chi tiết PANEL, màu áo đang dùng). */
  missingMaterials: MissingMaterial[];
};
export type MissingMaterial = {
  materialCode: string;
  /** FABRIC | TRIM. */
  suggestedType: string;
  colors: string[];
  /** Từ cột Tên NVL / Đơn vị của sheet NVL của đơn (nếu có). */
  suggestedName?: string | null;
  suggestedUnit?: string | null;
};
