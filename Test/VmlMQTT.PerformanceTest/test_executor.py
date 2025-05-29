# Sửa đổi cho file test_executor.py

import asyncio
import threading
import time
import json
import logging
import os
from concurrent.futures import ThreadPoolExecutor
from typing import Dict, List, Any, Callable, Optional, Tuple

from api_client import ApiClient, UserSession
from mqtt_client import MqttClient

# Thiết lập logging
logger = logging.getLogger("test_executor")

class TestResult:
    """Lưu trữ kết quả của một bài test"""
    
    def __init__(self):
        """Khởi tạo kết quả test"""
        self.total_users = 0
        self.successful_auth = 0
        self.successful_connect = 0
        self.connection_times = []
        self.messages_published = 0
        self.messages_received = 0
        self.errors = {}
        self.start_time = None
        self.end_time = None
        self.client_results = {}
    
    def calculate_success_rate(self) -> Dict[str, float]:
        """
        Tính toán tỷ lệ thành công
        
        Returns:
            Dict: Các tỷ lệ thành công khác nhau
        """
        auth_rate = 0
        connect_rate = 0
        overall_rate = 0
        
        if self.total_users > 0:
            auth_rate = (self.successful_auth / self.total_users) * 100
            connect_rate = (self.successful_connect / self.total_users) * 100
            overall_rate = (self.successful_connect / self.total_users) * 100
        
        return {
            "auth_rate": auth_rate,
            "connect_rate": connect_rate,
            "overall_rate": overall_rate
        }
    
    def to_dict(self) -> Dict[str, Any]:
        """
        Chuyển đổi kết quả test thành dictionary
        
        Returns:
            Dict: Kết quả test dưới dạng dictionary
        """
        duration = 0
        if self.start_time and self.end_time:
            duration = self.end_time - self.start_time
        
        avg_connection_time = 0
        if self.connection_times:
            avg_connection_time = sum(self.connection_times) / len(self.connection_times)
        
        return {
            "total_users": self.total_users,
            "successful_auth": self.successful_auth,
            "successful_connect": self.successful_connect,
            "success_rates": self.calculate_success_rate(),
            "duration": duration,
            "avg_connection_time": avg_connection_time,
            "messages_published": self.messages_published,
            "messages_received": self.messages_received,
            "errors": self.errors
        }

class UserWorker:
    """Thực hiện công việc cho một người dùng cụ thể"""
    
    def __init__(self, user: Dict[str, Any]):
        """
        Khởi tạo worker cho một user
        
        Args:
            user: Thông tin người dùng (id, phone)
        """
        self.user = user
        self.phone = user["phone"]
        self.user_id = user["id"]
        self.user_session = None
        self.mqtt_client = None
        self.error = None
    
    async def authenticate(self, use_cache: bool = True) -> bool:
        """
        Xác thực người dùng (login và start session)
        
        Args:
            use_cache: Có sử dụng cache không
            
        Returns:
            bool: True nếu xác thực thành công, False nếu thất bại
        """
        try:
            # Tạo client API
            api_client = ApiClient()
            
            # Tạo và khởi tạo phiên, sử dụng cache nếu được yêu cầu
            self.user_session = UserSession(phone=self.phone, user_id=self.user_id)
            logger.debug(f"Attempting to authenticate user {self.phone}")
            success = await self.user_session.initialize(api_client, use_cache=use_cache)
            
            # Đóng phiên API
            await api_client.close_session()
            
            if not success:
                self.error = self.user_session.error
                logger.error(f"Authentication failed for {self.phone}: {self.error}")
                return False
                
            logger.info(f"Authentication successful for {self.phone}")
            return True
            
        except Exception as e:
            self.error = f"Lỗi xác thực: {str(e)}"
            logger.error(f"Exception during authentication for {self.phone}: {str(e)}", exc_info=True)
            return False
    
    def connect_mqtt(self) -> bool:
        """
        Kết nối đến MQTT broker
        
        Returns:
            bool: True nếu kết nối thành công, False nếu thất bại
        """
        try:
            if not self.user_session:
                self.error = "Chưa xác thực hoặc không có thông tin session"
                logger.error(f"No user session for {self.phone} when trying to connect MQTT")
                return False
            
            # Lấy thông tin kết nối MQTT
            connection_info = self.user_session.get_mqtt_connection_info()
            if not connection_info:
                self.error = "Không có thông tin kết nối MQTT"
                logger.error(f"No MQTT connection info for {self.phone}")
                return False
                
            # Log thông tin kết nối
            logger.debug(f"MQTT connection info for {self.phone}: clientId={connection_info.get('clientId', 'N/A')[:10]}..., "
                        f"username={connection_info.get('username', 'N/A')[:10]}..., "
                        f"password={connection_info.get('password', 'N/A')[:10]}...")
            
            # Kiểm tra thông tin kết nối đầy đủ
            if not connection_info.get('clientId') or not connection_info.get('username') or not connection_info.get('password'):
                self.error = "Thiếu thông tin xác thực MQTT"
                logger.error(f"Missing MQTT credentials for {self.phone}")
                return False
            
            # Tạo và kết nối MQTT client
            self.mqtt_client = MqttClient(connection_info)
            success = self.mqtt_client.connect(timeout=30)
            
            if not success:
                self.error = f"Không thể kết nối đến MQTT broker: {self.mqtt_client.metrics.last_error}"
                logger.error(f"MQTT connection failed for {self.phone}: {self.mqtt_client.metrics.last_error}")
                return False
                
            logger.info(f"MQTT connected successfully for {self.phone}")
            return True
            
        except Exception as e:
            self.error = f"Lỗi kết nối MQTT: {str(e)}"
            logger.error(f"Exception during MQTT connection for {self.phone}: {str(e)}", exc_info=True)
            return False
    
    def publish_messages(self, count: int = 10, interval: float = 1.0, message_size: int = 100, qos: int = 1) -> bool:
        """
        Gửi nhiều tin nhắn
        
        Args:
            count: Số lượng tin nhắn cần gửi
            interval: Khoảng thời gian giữa các tin nhắn (giây)
            message_size: Kích thước tin nhắn (bytes)
            qos: Quality of Service (0, 1, 2)
            
        Returns:
            bool: True nếu gửi ít nhất một tin nhắn thành công, False nếu tất cả đều thất bại
        """
        if not self.mqtt_client:
            logger.error(f"No MQTT client for {self.phone} when trying to publish")
            return False
        
        success_count = 0
        
        for i in range(count):
            if self.mqtt_client.publish_message(message_size=message_size, qos=qos):
                success_count += 1
                logger.debug(f"Published message {i+1}/{count} for {self.phone}")
            else:
                logger.warning(f"Failed to publish message {i+1}/{count} for {self.phone}")
            time.sleep(interval)
        
        logger.info(f"Published {success_count}/{count} messages for {self.phone}")
        return success_count > 0
    
    def get_metrics(self) -> Dict[str, Any]:
        """
        Lấy metrics của worker
        
        Returns:
            Dict: Metrics của worker
        """
        result = {
            "phone": self.phone,
            "user_id": self.user_id,
            "authenticated": self.user_session is not None,
            "mqtt_connected": False,
            "error": self.error
        }
        
        # Thêm metrics từ MQTT client nếu có
        if self.mqtt_client:
            mqtt_metrics = self.mqtt_client.get_metrics()
            result["mqtt_connected"] = mqtt_metrics["is_connected"]
            result.update(mqtt_metrics)
        
        return result
    
    def disconnect(self):
        """Ngắt kết nối và giải phóng tài nguyên"""
        if self.mqtt_client:
            self.mqtt_client.disconnect()
            logger.debug(f"Disconnected MQTT client for {self.phone}")

class TestExecutor:
    """Thực thi các test case MQTT"""
    
    def __init__(self, config: Dict[str, Any]):
        """
        Khởi tạo test executor
        
        Args:
            config: Cấu hình cho test executor
        """
        self.config = config
        self.max_workers = config.get("max_workers", 10)
        self.test_result = TestResult()
        self.workers = {}  # Map phone -> UserWorker
        self.lock = threading.Lock()
    
    async def process_user(self, user: Dict[str, Any], use_cache: bool = True) -> Tuple[bool, bool]:
        """
        Xử lý một user hoàn chỉnh: xác thực và kết nối MQTT
        
        Args:
            user: Thông tin người dùng
            use_cache: Có sử dụng cache không
            
        Returns:
            Tuple[bool, bool]: (Kết quả xác thực, Kết quả kết nối MQTT)
        """
        phone = user["phone"]
        worker = UserWorker(user)
        
        # Lưu worker
        with self.lock:
            self.workers[phone] = worker
        
        # 1. Xác thực
        logger.debug(f"Starting authentication for {phone}")
        auth_success = await worker.authenticate(use_cache=use_cache)
        
        with self.lock:
            if auth_success:
                logger.info(f"Authentication successful for {phone}")
                self.test_result.successful_auth += 1
            else:
                error_type = "auth_error"
                if worker.error:
                    error_type = worker.error[:50]  # Lấy 50 ký tự đầu làm key
                
                if error_type in self.test_result.errors:
                    self.test_result.errors[error_type] += 1
                else:
                    self.test_result.errors[error_type] = 1
                    
                logger.error(f"Authentication failed for {phone}: {worker.error}")
                return (False, False)
        
        # 2. Kết nối MQTT nếu xác thực thành công
        mqtt_success = False
        if auth_success:
            mqtt_success = worker.connect_mqtt()
            
            with self.lock:
                if mqtt_success:
                    self.test_result.successful_connect += 1
                    
                    # Lưu thời gian kết nối
                    if worker.mqtt_client:
                        metrics = worker.mqtt_client.get_metrics()
                        if metrics.get("connection_time"):
                            self.test_result.connection_times.append(metrics["connection_time"])
                            
                    logger.info(f"MQTT connection successful for {phone}")
                else:
                    error_type = "mqtt_connect_error"
                    if worker.error:
                        error_type = worker.error[:50]
                    
                    if error_type in self.test_result.errors:
                        self.test_result.errors[error_type] += 1
                    else:
                        self.test_result.errors[error_type] = 1
                        
                    logger.error(f"MQTT connection failed for {phone}: {worker.error}")
        
        return (auth_success, mqtt_success)
    
    async def process_users(self, users: List[Dict[str, Any]], use_cache: bool = True) -> Dict[str, int]:
        """
        Xử lý nhiều người dùng song song: xác thực và kết nối MQTT
        
        Args:
            users: Danh sách người dùng
            use_cache: Có sử dụng cache không
            
        Returns:
            Dict[str, int]: Số lượng thành công cho mỗi bước
        """
        if not users:
            logger.error("Không có dữ liệu người dùng để xử lý")
            raise ValueError("Danh sách người dùng trống")
            
        self.test_result.total_users = len(users)
        self.test_result.start_time = time.time()
        
        logger.info(f"Bắt đầu xử lý {len(users)} người dùng")
        
        # Tạo các task xử lý
        tasks = []
        for user in users:
            # Kiểm tra dữ liệu hợp lệ
            if not user.get("phone"):
                logger.warning(f"Bỏ qua user không có số điện thoại: {user}")
                continue
                
            tasks.append(self.process_user(user, use_cache=use_cache))
        
        if not tasks:
            logger.error("Không có người dùng hợp lệ để xử lý")
            raise ValueError("Không có người dùng hợp lệ để xử lý")
        
        # Thực thi các task theo batch
        batch_size = min(self.max_workers, len(tasks))
        
        auth_success = 0
        mqtt_success = 0
        
        for i in range(0, len(tasks), batch_size):
            batch = tasks[i:i+batch_size]
            results = await asyncio.gather(*batch)
            
            # Đếm số lượng thành công
            for auth_ok, mqtt_ok in results:
                if auth_ok:
                    auth_success += 1
                if mqtt_ok:
                    mqtt_success += 1
            
            # Báo cáo tiến độ
            progress = (i + len(batch)) / len(tasks) * 100
            logger.info(f"Xử lý: {i + len(batch)}/{len(tasks)} ({progress:.1f}%)")
        
        logger.info(f"Đã xử lý {len(users)} người dùng: xác thực: {auth_success}, kết nối MQTT: {mqtt_success}")
        
        return {
            "auth_success": auth_success,
            "mqtt_success": mqtt_success
        }
    
    def run_publish_test(self, message_count: int = 10, interval: float = 1.0) -> Dict[str, int]:
        """
        Chạy test publish tin nhắn
        
        Args:
            message_count: Số lượng tin nhắn mỗi client gửi
            interval: Khoảng thời gian giữa các tin nhắn (giây)
            
        Returns:
            Dict: Kết quả test (messages_published, messages_received)
        """
        # Chỉ test với các client đã kết nối MQTT thành công
        connected_workers = [
            worker for phone, worker in self.workers.items()
            if worker.mqtt_client and worker.mqtt_client.get_metrics()["is_connected"]
        ]
        
        if not connected_workers:
            logger.warning("Không có client kết nối MQTT để test publish")
            return {"messages_published": 0, "messages_received": 0}
        
        logger.info(f"Bắt đầu test publish với {len(connected_workers)} client")
        
        # Lấy cấu hình test từ config
        message_size = self.config.get("message_size", 100)
        qos = self.config.get("qos", 1)
        
        with ThreadPoolExecutor(max_workers=self.max_workers) as executor:
            # Thực hiện publish cho tất cả các client
            futures = {
                executor.submit(
                    worker.publish_messages,
                    count=message_count,
                    interval=interval,
                    message_size=message_size,
                    qos=qos
                ): worker for worker in connected_workers
            }
            
            # Chờ hoàn thành
            for future in concurrent.futures.as_completed(futures):
                worker = futures[future]
                try:
                    success = future.result()
                    if not success:
                        logger.warning(f"Client {worker.phone} không thể gửi tin nhắn")
                except Exception as e:
                    logger.error(f"Lỗi khi publish tin nhắn cho {worker.phone}: {str(e)}", exc_info=True)
        
        # Chờ thêm một khoảng thời gian để nhận tin nhắn
        logger.info(f"Đã gửi tin nhắn, chờ {interval * 2} giây để nhận tin nhắn...")
        time.sleep(interval * 2)
        
        # Tổng hợp số liệu
        total_published = 0
        total_received = 0
        
        for worker in connected_workers:
            metrics = worker.mqtt_client.get_metrics()
            worker_published = metrics.get("messages_published", 0)
            worker_received = metrics.get("messages_received", 0)
            total_published += worker_published
            total_received += worker_received
            logger.debug(f"Worker {worker.phone}: published={worker_published}, received={worker_received}")
        
        with self.lock:
            self.test_result.messages_published = total_published
            self.test_result.messages_received = total_received
        
        logger.info(f"Kết quả test publish: Đã gửi {total_published}, đã nhận {total_received} tin nhắn")
        return {
            "messages_published": total_published,
            "messages_received": total_received
        }
    
    def disconnect_all(self):
        """Ngắt kết nối tất cả client"""
        logger.info("Ngắt kết nối tất cả client...")
        
        with ThreadPoolExecutor(max_workers=self.max_workers) as executor:
            list(executor.map(lambda worker: worker.disconnect(), self.workers.values()))
    
    def collect_client_results(self):
        """Thu thập kết quả từ tất cả client"""
        for phone, worker in self.workers.items():
            self.test_result.client_results[phone] = worker.get_metrics()
    
    def get_test_result(self) -> Dict[str, Any]:
        """
        Lấy kết quả test
        
        Returns:
            Dict: Kết quả test
        """
        self.test_result.end_time = time.time()
        self.collect_client_results()
        
        # In thông tin tổng hợp kết quả để debug
        logger.debug(f"Final test result: {json.dumps(self.test_result.to_dict(), indent=2)}")
        
        return self.test_result.to_dict()
    
    async def run_test_scenario(self, users: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        Chạy một kịch bản test hoàn chỉnh
        
        Args:
            users: Danh sách người dùng để test
            
        Returns:
            Dict: Kết quả test
        """
        try:
            logger.info(f"Bắt đầu test với {len(users)} người dùng")
            
            # Xác thực và kết nối MQTT cho mỗi user (quy trình liên tục)
            use_cache = self.config.get("use_cache", True)
            await self.process_users(users, use_cache=use_cache)
            
            # Test publish/subscribe nếu có kết nối thành công
            if self.test_result.successful_connect > 0:
                message_count = self.config.get("message_count", 10)
                message_interval = self.config.get("message_interval", 1.0)
                self.run_publish_test(message_count, message_interval)
            
            # Thu thập kết quả
            result = self.get_test_result()
            
            # Lưu kết quả
            self.save_result(result)
            
            return result
        finally:
            # Ngắt kết nối tất cả client
            self.disconnect_all()
    
    def save_result(self, result: Dict[str, Any]):
        """
        Lưu kết quả test vào file
        
        Args:
            result: Kết quả test
        """
        result_dir = "results"
        os.makedirs(result_dir, exist_ok=True)
        
        timestamp = time.strftime("%Y%m%d_%H%M%S")
        filename = os.path.join(result_dir, f"test_result_{timestamp}.json")
        
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(result, f, indent=2)
        
        logger.info(f"Đã lưu kết quả test vào {filename}")

# Đảm bảo import đúng module
import concurrent.futures