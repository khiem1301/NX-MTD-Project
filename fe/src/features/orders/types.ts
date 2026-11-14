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
