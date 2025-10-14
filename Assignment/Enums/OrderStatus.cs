namespace Assignment.Enums
{
    public enum OrderStatus
    {
        Pending, // Đối với đơn hàng mới tạo
        Paid, // Đối với đơn hàng đã thanh toán thành công
        Processing, // Đối với đơn hàng đang được xử lý
        CodProcessing, // Đối với đơn hàng COD đang được xử lý
        CodShipped, // Đối với đơn hàng COD đã được giao cho đơn vị vận chuyển
        CodPaymentReceived, // Đối với đơn hàng COD đã nhận được thanh toán từ khách hàng
        Completed, // Đối với đơn hàng đã hoàn thành và giao hàng thành công
        Cancelled // Đối với đơn hàng đã bị hủy
    }
}
